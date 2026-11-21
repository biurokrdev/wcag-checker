using D2.Wcag.Docx;
using D2.Wcag.Standards;

namespace D2.Wcag.Tests.Corporate20;

// DoD Case 2: każde zgłoszenie standardu firmowy/2.0 – także o kodzie sprzed rejestru reguł i o kodzie wspólnym
// z WCAG albo standardem 1.9 – ma instrukcję naprawy krok po kroku, a status tam, gdzie specyfikacja go podaje
// i gdzie zgadza się z dotychczasową wagą. Wyniki wcag/2.1 i firmowy/1.9 zostają bez tych pól.
public class FixInstructionTests
{
    private const SpecStatus Required = SpecStatus.Required;
    private const SpecStatus Recommended = SpecStatus.Recommended;
    private const SpecStatus Conditional = SpecStatus.Conditional;

    // Jawna tabela kod → status w standardzie 2.0. null = bez statusu: specyfikacja go nie podaje (tabele typografii,
    // listy, stopka, WZÓR, logo) albo podaje status, który według SpecStatuses.Severity dałby inną wagę niż
    // dotychczasowa (decyzje użytkownika o ostrzeżeniach; kody z dwiema wagami).
    private static readonly Dictionary<string, SpecStatus?> ExpectedStatus = new()
    {
        // G-01
        [IssueCodes.PageFormatNotA4] = Required,
        [IssueCodes.MarginsMissing] = Required,
        [IssueCodes.InvalidMargins] = Required,
        // rozdz. 1
        [IssueCodes.JustificationForbidden] = Required,
        [IssueCodes.FontFamilyMismatch] = Required,
        [IssueCodes.FontWeightInFamilyName] = Required,
        [IssueCodes.LineSpacingMismatch] = Required,
        // 10 pt WYMAGANE (błąd), 12/14 pt WARUNKOWE (ostrzeżenie zamiast informacji): jeden kod, dwie wagi
        [IssueCodes.BodyTextSizeMismatch] = null,
        // G-02: ostrzeżenia z decyzji użytkownika
        [IssueCodes.TextColorNotAllowed] = null,
        [IssueCodes.TextColorUsageMismatch] = null,
        // rozdz. 3 i 4: tabele typografii i odstępów bez kolumny statusu
        [IssueCodes.HeadingStyleMismatch] = null,
        [IssueCodes.HeadingSpacingMismatch] = null,
        [IssueCodes.LinkStyleMismatch] = null,
        [Corporate20Codes.LayoutHeadingSize] = Recommended,
        [Corporate20Codes.LayoutEffectiveDateSize] = Recommended,
        [Corporate20Codes.RegistrationDataPlacement] = Conditional,
        // rozdz. 6: spis powyżej 9 stron WARUNKOWE (ostrzeżenie zamiast informacji); nagłówek spisu WYMAGANE
        [IssueCodes.TocRequired] = null,
        [IssueCodes.TocHeadingInToc] = Required,
        [IssueCodes.TocHeadingStyleMismatch] = Required,
        // rozdz. 7: bez statusów
        [IssueCodes.ListBulletColorMismatch] = null,
        [IssueCodes.ListNumberNotBold] = null,
        // G-07, rozdz. 16
        [IssueCodes.HeadingLevelSkipped] = Required,
        // rozdz. 9; tytuł tabeli „WYMAGANE / BIZNES” – ostrzeżenie z decyzji użytkownika; opis REKOMENDOWANE, a jest informacją
        [IssueCodes.TableMissingHeaders] = Required,
        [IssueCodes.TableEmptyCell] = Required,
        [IssueCodes.TableRowCanSplit] = Required,
        [IssueCodes.TableBorderMismatch] = Required,
        [IssueCodes.TableHeaderNotBold] = Required,
        [IssueCodes.TableAlignmentMismatch] = Required,
        [IssueCodes.TableTitleMissing] = null,
        [IssueCodes.TableDescriptionMissing] = null,
        // rozdz. 5: bez statusu
        [IssueCodes.BrandImageMismatch] = null,
        // rozdz. 11 (tabela bez statusów; braki są błędami z decyzji użytkownika), rozdz. 10 WYMAGANE
        [IssueCodes.FooterStyleMismatch] = null,
        [IssueCodes.FooterTitleMissing] = Required,
        [IssueCodes.TitlePageFooterPresent] = null,
        [Corporate20Codes.FooterMissing] = Required,
        [Corporate20Codes.FooterPageNumberMissing] = Required,
        [Corporate20Codes.FooterFormat] = Recommended,
        [Corporate20Codes.RegistrationDataFormat] = Required,
        // rozdz. 17 (brak tytułu i autora: decyzja użytkownika; tabela formatu bez statusów), rozdz. 18 WYMAGANE
        [IssueCodes.TitleMissing] = Required,
        [IssueCodes.TitleGeneric] = null,
        [IssueCodes.TitleSuffixInvalid] = null,
        [IssueCodes.AuthorMissing] = Required,
        [IssueCodes.AuthorNotInstitution] = null,
        [IssueCodes.LanguageMissing] = Required,
        [IssueCodes.LanguageInvalid] = Required,
        // rozdz. 14
        [IssueCodes.VariantMarkerUnbalanced] = Required,
        [IssueCodes.VariantMarkerColorMismatch] = Required,
        [IssueCodes.VariableColorMismatch] = Required,
        [IssueCodes.VariantInflectionShortcut] = Required,
        [IssueCodes.VariantSlashAlternative] = Required,
        // rozdz. 15: bez statusów
        [IssueCodes.SampleMarkStyleMismatch] = null,
        [IssueCodes.SampleMarkNotOnEveryPage] = null,
    };

    private static IEnumerable<CorporateStandard20> Standards() =>
        new DocumentLayout20?[] { null }.Concat(Enum.GetValues<DocumentLayout20>().Cast<DocumentLayout20?>())
            .Select(layout => new CorporateStandard20(new CorporateStandard20Options { Layout = layout }));

    private static void AssertSteps(string? fix, string context)
    {
        Assert.False(string.IsNullOrWhiteSpace(fix), context + ": brak instrukcji naprawy");
        Assert.StartsWith("1. ", fix);
        Assert.Contains(" 2. ", fix);
        Assert.DoesNotContain("{", fix);
        Assert.EndsWith(".", fix);
    }

    [Fact]
    public void StatusTable_MatchesTheStandard_AndCoversEveryCode()
    {
        foreach (var standard in Standards())
        {
            var codes = standard.Checks.SelectMany(c => c.Codes).ToHashSet();
            Assert.Empty(codes.Except(ExpectedStatus.Keys));

            var actual = standard.LegacyStatuses.ToDictionary(p => p.Key, p => (SpecStatus?)p.Value);
            foreach (var descriptor in standard.Descriptors)
            {
                actual[descriptor.Code] = descriptor.Status;
            }

            // Reguły układu dokumentu (F20-…) są w standardzie tylko po wyborze układu.
            Assert.All(
                codes.Where(code => !code.StartsWith("F20-", StringComparison.Ordinal) || actual.ContainsKey(code)),
                code => Assert.Equal(ExpectedStatus[code], actual.GetValueOrDefault(code)));
            Assert.Empty(actual.Keys.Except(ExpectedStatus.Keys));
        }

        Assert.Empty(ExpectedStatus.Keys.Except(Standards().SelectMany(s => s.Checks.SelectMany(c => c.Codes))));
    }

    [Fact]
    public void EveryCodeOfStandard20_HasStepByStepFix_FromDescriptorOrLegacyTable()
    {
        foreach (var standard in Standards())
        {
            var described = standard.Descriptors.ToDictionary(d => d.Code, d => d.Fix);
            var legacy = standard.LegacyFixes;

            // Każdy kod ma dokładnie jedno źródło instrukcji: deskryptor nowej reguły albo tabelę dla starych kodów.
            Assert.Empty(described.Keys.Intersect(legacy.Keys));
            foreach (var code in standard.Checks.SelectMany(c => c.Codes))
            {
                if (code.StartsWith("F20-", StringComparison.Ordinal) && !described.ContainsKey(code))
                {
                    continue; // reguła układu dokumentu nieaktywna bez wybranego układu
                }

                Assert.True(described.ContainsKey(code) || legacy.ContainsKey(code), $"Kod {code} nie ma instrukcji naprawy.");
            }

            Assert.All(legacy, pair => AssertSteps(pair.Value, pair.Key));
            Assert.All(legacy.Keys, code => Assert.Contains(code, ExpectedStatus.Keys));
        }
    }

    [Fact]
    public void LegacyFixes_UseTheConfiguredInstitution()
    {
        var standard = new CorporateStandard20("Inna Firma S. A.");

        Assert.Contains("„[Tytuł dokumentu]. Inna Firma S. A.”", standard.LegacyFixes[IssueCodes.TitleMissing]);
        Assert.Contains("„Inna Firma S. A.”", standard.LegacyFixes[IssueCodes.AuthorNotInstitution]);
        Assert.DoesNotContain(standard.LegacyFixes.Values, fix => fix.Contains(CorporateStandard20.DefaultInstitution));
    }

    // Wszystkie dokumenty testowe (fixtury i pliki z samples/), bez układu i z każdym układem dokumentu.
    [Fact]
    public void EveryIssueOfStandard20_OnAllTestDocuments_HasFix_AndStatusFromTheTable()
    {
        var seen = new HashSet<string>();
        foreach (var name in SnapshotCases.Documents)
        {
            var bytes = SnapshotCases.Load(name);
            foreach (var standard in Standards())
            {
                var result = Audit20.Validate(bytes, new CorporateStandard20Options { Layout = standard.Layout });
                Assert.All(result.Issues, issue =>
                {
                    AssertSteps(issue.Fix, $"{name}: {issue.Code}");
                    Assert.True(ExpectedStatus.TryGetValue(issue.Code, out var status), $"{name}: kod {issue.Code} spoza tabeli.");
                    Assert.Equal(status, issue.Status);
                    seen.Add(issue.Code);
                });
            }
        }

        Assert.True(seen.Count >= 30, $"Dokumenty testowe wywołały tylko {seen.Count} kodów.");
    }

    [Fact]
    public void Gaps_OldCodes_NowCarryStatusAndFix_WithUnchangedSeverityAndMessage()
    {
        var gaps = Audit20.Gaps;

        var headerNotBold = Assert.Single(gaps.Of(IssueCodes.TableHeaderNotBold));
        Assert.Equal((IssueSeverity.Error, (SpecStatus?)Required), (headerNotBold.Severity, headerNotBold.Status));
        Assert.Equal("Tabela 2: 1 komórka nagłówka nie jest pogrubiona.", headerNotBold.Message);
        Assert.Contains("Pogrubienie (Ctrl+B)", headerNotBold.Fix);
        Assert.Null(headerNotBold.Title);

        var notBold = Assert.Single(gaps.Of(IssueCodes.ListNumberNotBold));
        Assert.Equal((IssueSeverity.Warning, (SpecStatus?)null), (notBold.Severity, notBold.Status));
        Assert.Contains("Zdefiniuj nowy format numeracji", notBold.Fix);

        var slash = Assert.Single(gaps.Of(IssueCodes.VariantSlashAlternative));
        Assert.Contains("„[lub]”", slash.Fix);
        Assert.Contains("Powiązane osoby", Assert.Single(gaps.Of(IssueCodes.AuthorMissing)).Fix);
    }

    [Fact]
    public void WcagAndStandard19_GetNoFixAndNoStatus()
    {
        var validator = new WcagDocxValidator();
        foreach (var name in SnapshotCases.Documents)
        {
            var bytes = SnapshotCases.Load(name);
            var others = validator.Validate(bytes, new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/1.9"));
            Assert.All(others.Issues, i => Assert.Equal(((string?)null, (SpecStatus?)null), (i.Fix, i.Status)));

            // Przy profilu łączonym pola dostają tylko wpisy standardu 2.0; wpis scalony z WCAG (ta sama waga) zostaje wpisem WCAG.
            var both = validator.Validate(bytes, new WcagValidationProfile().WithStandards("wcag/2.1", Audit20.Key));
            Assert.All(both.For("wcag/2.1"), i => Assert.Equal(((string?)null, (SpecStatus?)null), (i.Fix, i.Status)));
            Assert.All(both.For(Audit20.Key), i => AssertSteps(i.Fix, $"{name}: {i.Code}"));
        }

        Assert.Empty(StandardCatalog.Default.Get("wcag/2.1").LegacyFixes);
        Assert.Empty(StandardCatalog.Default.Get("firmowy/1.9").LegacyFixes);
    }
}
