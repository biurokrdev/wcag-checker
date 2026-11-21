using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using D2.Wcag.Api.Workbooks;
using D2.Wcag.Packages;
using D2.Wcag.Tests;
using D2.Wcag.Xlsx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using static D2.Wcag.Api.Tests.ApiTestHost;
using static D2.Wcag.Api.Tests.Workbooks.WorkbookTestData;

namespace D2.Wcag.Api.Tests.Workbooks;

// POST api/wcag-workbooks/validate i GET criteria. Asercje dotyczą struktury odpowiedzi i jej spójności, a nie konkretnych
// kodów reguł – te należą do testów biblioteki i zmieniają się wraz z dopisywanymi grupami reguł.
public class WorkbookValidateApiTests : IClassFixture<DefaultApiTestHost>
{
    private static readonly string[] StatusLabels = ["Niespełnione", "Brak wykrytych problemów", "Do oceny ręcznej", "Nie dotyczy"];

    private readonly DefaultApiTestHost _host;
    private readonly HttpClient _client;

    public WorkbookValidateApiTests(DefaultApiTestHost host)
    {
        _host = host;
        _client = host.CreateClient();
    }

    public static IEnumerable<object[]> Cases() => XlsxSnapshotCases.All();

    private Task<HttpResponseMessage> Post(string fixture, string? version = null) =>
        _client.PostAsync(Validate, version is null
            ? Form(Fixture(fixture), FixtureFile(fixture))
            : Form(Fixture(fixture), FixtureFile(fixture), ("wcagVersion", version)));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Validate_EveryFixture_ReturnsAConsistentResult(string fixture, string version)
    {
        var response = await Post(fixture, version);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<WorkbookValidationResponse>(raw, Json)!;

        // Nagłówek: raport, plik, profil.
        Assert.False(string.IsNullOrWhiteSpace(result.ReportId));
        Assert.Equal(FixtureFile(fixture), result.File.FileName);
        Assert.Equal(Fixture(fixture).LongLength, result.File.SizeBytes);
        Assert.True(OfficePackage.IsReadableSpreadsheet(result.File.Format));
        Assert.Equal(version, result.Profile.WcagVersion);
        Assert.Equal($"WCAG {version} AA (arkusz Excel)", result.Profile.ProfileName);

        // Raport nigdy nie stwierdza zgodności.
        Assert.Equal("Wynik automatyczny nie potwierdza zgodności z WCAG", result.Disclaimer);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        Assert.DoesNotMatch(ConformanceClaim(), result.Summary);

        // Liczby spójne z listami.
        Assert.Equal(result.Findings.Count(f => f.Severity == XlsxSeverity.Error), result.Counts.Errors);
        Assert.Equal(result.Findings.Count(f => f.Severity == XlsxSeverity.Warning), result.Counts.Warnings);
        Assert.Equal(result.Findings.Count(f => f.Severity == XlsxSeverity.NeedsReview), result.Counts.NeedsReview);
        Assert.Equal(result.Findings.Count(f => f.Severity == XlsxSeverity.Info), result.Counts.Info);
        Assert.Equal(result.ManualChecks.Count, result.Counts.ManualChecks);

        // Zgłoszenia.
        Assert.Equal(Enumerable.Range(0, result.Findings.Count), result.Findings.Select(f => f.Index));
        Assert.All(result.Findings, f =>
        {
            Assert.StartsWith("XL-", f.Code);
            Assert.False(string.IsNullOrWhiteSpace(f.Title));
            Assert.False(string.IsNullOrWhiteSpace(f.Message));
            Assert.False(string.IsNullOrWhiteSpace(f.FixInstruction));
            Assert.Equal(WorkbookLabels.Severity(f.Severity), f.SeverityLabel);
            Assert.Equal(WorkbookLabels.CheckType(f.CheckType), f.CheckTypeLabel);
            Assert.NotEqual(XlsxCheckType.Manual, f.CheckType);
            Assert.False(string.IsNullOrWhiteSpace(f.Location.Display));
            Assert.False(string.IsNullOrWhiteSpace(f.Location.Key));
            Assert.True(f.MoreCount >= 0);
            Assert.Equal(f.Wcag.Count == 0, f.BestPractice);
            if (f.BestPractice)
            {
                Assert.Equal(XlsxSeverity.Info, f.Severity);
            }

            if (f.Location.Sheet is { } sheet)
            {
                Assert.Contains(sheet, result.Sheets);
            }

            Assert.All(f.Wcag, AssertReference);
        });

        // Checklista.
        Assert.Equal(result.ManualChecks.Count, result.ManualChecks.Select(c => c.Id).Distinct().Count());
        Assert.All(result.ManualChecks, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Id));
            Assert.False(string.IsNullOrWhiteSpace(c.Question));
            Assert.NotEmpty(c.Wcag);
            Assert.All(c.Wcag, AssertReference);
        });

        // Macierz kryteriów: komplet dla wersji, cztery statusy z polskimi etykietami, „Niespełnione” tylko z błędem.
        Assert.Equal(version == "2.2" ? 55 : 50, result.Criteria.Count);
        Assert.Equal(result.Criteria.Count, result.Criteria.Select(c => c.Sc).Distinct().Count());
        Assert.All(result.Criteria, c =>
        {
            Assert.Contains(c.StatusLabel, StatusLabels);
            Assert.Equal(XlsxCriterionResult.StatusLabel(c.Status), c.StatusLabel);
            Assert.Contains(c.Level, new[] { "A", "AA" });
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Reason));
            Assert.StartsWith("https://www.w3.org/WAI/WCAG22/Understanding/", c.Url);
            Assert.Equal(c.ManualChecks > 0, result.ManualChecks.Any(m => m.Wcag.Any(w => w.Sc == c.Sc)));
            var hasError = result.Findings.Any(f => f.Severity == XlsxSeverity.Error && f.Wcag.Any(w => w.Sc == c.Sc));
            Assert.Equal(hasError, c.Status == XlsxCriterionStatus.Failed);
        });
        Assert.Contains(result.Criteria, c => c.SoftwareDependent);

        // JSON: camelCase, enumy jako nazwy, pola bez wartości pominięte.
        Assert.DoesNotContain(":null", raw);
        Assert.Contains("\"reportId\":\"", raw);
        Assert.Contains("\"wcagVersion\":\"" + version + "\"", raw);
        Assert.Matches("\"status\":\"(Failed|NoIssuesDetected|ManualReview|NotApplicable)\"", raw);
        Assert.DoesNotMatch("\"(status|severity|checkType|kind|format)\":\\d", raw);
    }

    private static void AssertReference(WorkbookWcagRefDto reference)
    {
        Assert.Matches(@"^\d\.\d\.\d{1,2}$", reference.Sc);
        Assert.Contains(reference.Level, new[] { "A", "AA" });
        Assert.False(string.IsNullOrWhiteSpace(reference.Name));
        Assert.StartsWith("https://www.w3.org/WAI/WCAG22/Understanding/", reference.Url);
    }

    [Fact]
    public async Task Validate_DefaultsToWcag22_AndVersionsDifferInCriteria()
    {
        var byDefault = await Body<WorkbookValidationResponse>(await Post("clean"));
        var v22 = await Body<WorkbookValidationResponse>(await Post("clean", "2.2"));
        var v21 = await Body<WorkbookValidationResponse>(await Post("clean", "2.1"));

        Assert.Equal("2.2", byDefault.Profile.WcagVersion);
        Assert.Equal(v22.Criteria.Select(c => c.Sc), byDefault.Criteria.Select(c => c.Sc));

        Assert.DoesNotContain(v22.Criteria, c => c.Sc == "4.1.1");
        Assert.All(NewIn22, sc => Assert.Contains(v22.Criteria, c => c.Sc == sc));

        var parsing = Assert.Single(v21.Criteria, c => c.Sc == "4.1.1");
        Assert.Equal(XlsxCriterionStatus.NotApplicable, parsing.Status);
        Assert.All(NewIn22, sc => Assert.DoesNotContain(v21.Criteria, c => c.Sc == sc));
        Assert.All(NewIn22, sc => Assert.DoesNotContain(v21.ManualChecks, m => m.Wcag.Any(w => w.Sc == sc)));
    }

    [Theory]
    [InlineData("3.0")]
    [InlineData("2.0")]
    [InlineData("wcag")]
    public async Task Validate_RejectsUnknownWcagVersion(string version)
    {
        var response = await Post("clean", version);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Body<ProblemDetails>(response);
        Assert.Equal("Nieznana wersja WCAG.", problem.Title);
        Assert.Equal($"Nieznana wersja WCAG „{version}”; dostępne: 2.2, 2.1.", problem.Detail);
    }

    [Fact]
    public async Task Validate_IsDeterministic_ExceptForTheReportId()
    {
        static string WithoutReportId(string json) => Regex.Replace(json, "\"reportId\":\"[0-9a-f]+\"", "\"reportId\":\"*\"");

        foreach (var fixture in new[] { "structure", "macro", "empty" })
        {
            var first = await (await Post(fixture, "2.2")).Content.ReadAsStringAsync();
            var second = await (await Post(fixture, "2.2")).Content.ReadAsStringAsync();

            Assert.NotEqual(first, second);
            Assert.Equal(WithoutReportId(first), WithoutReportId(second));
        }
    }

    [Fact]
    public async Task Validate_MacroWorkbook_IsAudited_AndSaysMacrosAreNotAssessed()
    {
        var result = await Body<WorkbookValidationResponse>(await Post("macro"));

        Assert.Equal(OfficePackageKind.SpreadsheetMacro, result.File.Format);
        Assert.Equal("macro.xlsm", result.File.FileName);
        Assert.True(result.Workbook.HasMacros);
        Assert.Contains(result.Findings, f => f.BestPractice && f.Message.Contains("makra", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validate_EmptyWorkbook_IsReadable_AndSaysThereIsNothingToAssess()
    {
        var result = await Body<WorkbookValidationResponse>(await Post("empty"));

        Assert.True(result.Workbook.IsEmpty);
        Assert.StartsWith("Skoroszyt jest pusty", result.Summary);
        Assert.Equal(0, result.Counts.Errors);
    }

    [Fact]
    public async Task Validate_NormalizesTheFileName_AndKeepsPolishCharacters()
    {
        var response = await _client.PostAsync(Validate, Form(Fixture("clean"), "C:\\Dane\\Budżet 2026.xlsx"));

        var result = await Body<WorkbookValidationResponse>(response);
        Assert.Equal("Budżet 2026.xlsx", result.File.FileName);
    }

    [Fact]
    public async Task Validate_PassesTheFileNameAndVersionToTheAuditProfile_AndTheRequestCancellationToken()
    {
        var auditor = new RecordingAuditor();
        var service = new WorkbookAuditService(auditor, _host.Services.GetRequiredService<IWorkbookReportStore>());
        using var cancellation = new CancellationTokenSource();

        service.Validate("zamówienia.xlsx", Fixture("clean"), "2.1", cancellation.Token);

        Assert.Equal("zamówienia.xlsx", auditor.Profile!.FileName);
        Assert.Equal("2.1", auditor.Profile.WcagVersion);
        Assert.Equal(20L * 1024 * 1024, auditor.Profile.Limits.MaxFileBytes);
        Assert.Equal(cancellation.Token, auditor.Token);

        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => service.Validate("zamówienia.xlsx", Fixture("edge"), "2.2", cancellation.Token));
    }

    [Fact]
    public void Validate_LeavesTheInputBytesUntouched()
    {
        using var scope = _host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IWorkbookAuditService>();

        foreach (var (name, _) in XlsxSnapshotCases.Fixtures)
        {
            var content = Fixture(name);
            var copy = (byte[])content.Clone();

            service.Validate(FixtureFile(name), content, "2.2", CancellationToken.None);

            Assert.True(copy.AsSpan().SequenceEqual(content), $"Audyt zmienił bajty pliku {name}.");
        }
    }

    [Theory]
    [InlineData("2.2", 55)]
    [InlineData("2.1", 50)]
    public async Task Criteria_ReturnsTheCatalogForTheVersion_WithoutAFile(string version, int expected)
    {
        var response = await _client.GetAsync($"{Criteria}?version={version}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var catalog = JsonSerializer.Deserialize<WorkbookCriteriaResponse>(raw, Json)!;
        Assert.Equal(version, catalog.WcagVersion);
        Assert.Equal($"WCAG {version} AA (arkusz Excel)", catalog.ProfileName);
        Assert.Equal("Wynik automatyczny nie potwierdza zgodności z WCAG", catalog.Disclaimer);
        Assert.Equal(expected, catalog.Criteria.Count);
        Assert.Equal(version == "2.1", catalog.Criteria.Any(c => c.Sc == "4.1.1"));
        Assert.Equal(version == "2.2", catalog.Criteria.Any(c => c.Sc == "2.5.8"));
        Assert.All(catalog.Criteria, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Reason));
            Assert.False(string.IsNullOrWhiteSpace(c.ApplicabilityLabel));
            Assert.StartsWith("https://www.w3.org/WAI/WCAG22/Understanding/", c.Url);
        });
        Assert.Contains(catalog.Criteria, c => c.SoftwareDependent && c.ApplicabilityLabel == "Zależy od oprogramowania");
        Assert.DoesNotContain(":null", raw);
    }

    [Fact]
    public async Task Criteria_DefaultsTo22_AndRejectsUnknownVersion()
    {
        var byDefault = await Body<WorkbookCriteriaResponse>(await _client.GetAsync(Criteria));
        var unknown = await _client.GetAsync($"{Criteria}?version=1.0");

        Assert.Equal("2.2", byDefault.WcagVersion);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("Nieznana wersja WCAG.", (await Body<ProblemDetails>(unknown)).Title);
    }

    private sealed class RecordingAuditor : IWcagXlsxAuditor
    {
        private readonly WcagXlsxAuditor _inner = new();

        public XlsxAuditProfile? Profile { get; private set; }

        public CancellationToken Token { get; private set; }

        public XlsxAuditResult Audit(byte[] content, XlsxAuditProfile? profile = null, CancellationToken cancellationToken = default)
        {
            Profile = profile;
            Token = cancellationToken;
            return _inner.Audit(content, profile, cancellationToken);
        }

        public XlsxAuditResult Audit(Stream stream, XlsxAuditProfile? profile = null, CancellationToken cancellationToken = default) =>
            _inner.Audit(stream, profile, cancellationToken);
    }
}
