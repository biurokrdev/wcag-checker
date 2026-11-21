using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Corporate20;

// Case 2, pkt 2: jedna tabela status → waga (SpecStatuses.Severity). WYMAGANE to w standardzie 2.0 błąd – także dla
// kodów wspólnych z WCAG i standardem 1.9 (G-01, G-07, ukośnik zamiast [lub], tytuł i autor), gdzie tamte standardy
// zostają przy swojej wadze. Paleta (G-02), „WZÓR nie na każdej stronie” (G-16) i tytuł tabeli zostają ostrzeżeniami.
public class SeverityTableTests
{
    private const IssueSeverity Error = IssueSeverity.Error;
    private const IssueSeverity Warning = IssueSeverity.Warning;
    private const IssueSeverity Info = IssueSeverity.Info;

    // Cała tabela standardu 2.0: kod → wagi, z którymi wolno mu się pojawić. Błąd = wymaganie WYMAGANE w specyfikacji.
    private static readonly Dictionary<string, IssueSeverity[]> Expected = new()
    {
        // G-01
        [IssueCodes.PageFormatNotA4] = [Error],
        [IssueCodes.MarginsMissing] = [Error],
        [IssueCodes.InvalidMargins] = [Error],
        // rozdz. 1
        [IssueCodes.JustificationForbidden] = [Error],
        [IssueCodes.FontFamilyMismatch] = [Error],
        [IssueCodes.FontWeightInFamilyName] = [Error],
        [IssueCodes.LineSpacingMismatch] = [Error],
        // tekst 10 pt: WYMAGANE; powiększenie do 12/14 pt: WARUNKOWE, zgłaszane jako ostrzeżenie
        [IssueCodes.BodyTextSizeMismatch] = [Error, Warning],
        // G-02: decyzja użytkownika – ostrzeżenia
        [IssueCodes.TextColorNotAllowed] = [Warning],
        [IssueCodes.TextColorUsageMismatch] = [Warning],
        // G-03, G-05, łącza
        [IssueCodes.HeadingStyleMismatch] = [Error],
        [IssueCodes.HeadingSpacingMismatch] = [Warning],
        [Corporate20Codes.LayoutHeadingSize] = [Warning],
        [Corporate20Codes.LayoutEffectiveDateSize] = [Warning],
        [Corporate20Codes.RegistrationDataPlacement] = [Info],
        [IssueCodes.LinkStyleMismatch] = [Error],
        // G-06 (warunkowe), nagłówek spisu treści (wymagane)
        [IssueCodes.TocRequired] = [Warning],
        [IssueCodes.TocHeadingInToc] = [Error],
        [IssueCodes.TocHeadingStyleMismatch] = [Error],
        // rozdz. 7: kolor punktora, pogrubienie numeru („wytyczna ze slajdu”)
        [IssueCodes.ListBulletColorMismatch] = [Error],
        [IssueCodes.ListNumberNotBold] = [Warning],
        // G-07
        [IssueCodes.HeadingLevelSkipped] = [Error],
        // G-08, G-09; tytuł tabeli: decyzja użytkownika – ostrzeżenie; opis: informacja
        [IssueCodes.TableMissingHeaders] = [Error],
        [IssueCodes.TableEmptyCell] = [Error],
        [IssueCodes.TableRowCanSplit] = [Error],
        [IssueCodes.TableBorderMismatch] = [Error],
        [IssueCodes.TableHeaderNotBold] = [Error],
        [IssueCodes.TableAlignmentMismatch] = [Error],
        [IssueCodes.TableTitleMissing] = [Warning],
        [IssueCodes.TableDescriptionMissing] = [Info],
        // G-10
        [IssueCodes.BrandImageMismatch] = [Warning],
        // G-11 i rozdz. 10
        [IssueCodes.FooterStyleMismatch] = [Error],
        [IssueCodes.FooterTitleMissing] = [Error],
        [IssueCodes.TitlePageFooterPresent] = [Warning],
        [Corporate20Codes.FooterMissing] = [Error],
        [Corporate20Codes.FooterPageNumberMissing] = [Error],
        [Corporate20Codes.FooterFormat] = [Warning],
        [Corporate20Codes.RegistrationDataFormat] = [Error],
        // G-12, G-13
        [IssueCodes.TitleMissing] = [Error],
        [IssueCodes.TitleGeneric] = [Warning],
        [IssueCodes.TitleSuffixInvalid] = [Error],
        [IssueCodes.AuthorMissing] = [Error],
        [IssueCodes.AuthorNotInstitution] = [Error],
        [IssueCodes.LanguageMissing] = [Error],
        [IssueCodes.LanguageInvalid] = [Error],
        // G-14
        [IssueCodes.VariantMarkerUnbalanced] = [Error],
        [IssueCodes.VariantMarkerColorMismatch] = [Error],
        [IssueCodes.VariableColorMismatch] = [Error],
        [IssueCodes.VariantInflectionShortcut] = [Error],
        [IssueCodes.VariantSlashAlternative] = [Error],
        // G-16: styl napisu WZÓR wymagany; „nie na każdej stronie” – decyzja użytkownika – ostrzeżenie
        [IssueCodes.SampleMarkStyleMismatch] = [Error],
        [IssueCodes.SampleMarkNotOnEveryPage] = [Warning],
    };

    private static CorporateStandard20 Standard(DocumentLayout20? layout = null) => new(new CorporateStandard20Options { Layout = layout });

    private static IEnumerable<DocumentLayout20?> Layouts() => [null, .. Enum.GetValues<DocumentLayout20>().Cast<DocumentLayout20?>()];

    [Theory]
    [InlineData(SpecStatus.Required, IssueSeverity.Error)]
    [InlineData(SpecStatus.Recommended, IssueSeverity.Warning)]
    [InlineData(SpecStatus.Conditional, IssueSeverity.Info)]
    [InlineData(SpecStatus.ToVerify, IssueSeverity.Info)]
    public void StatusMapsToSeverity_AndTheTableCoversEveryStatus(SpecStatus status, IssueSeverity severity)
    {
        Assert.Equal(severity, SpecStatuses.Severity(status));
        Assert.Equal(4, Enum.GetValues<SpecStatus>().Length);
    }

    [Fact]
    public void EveryCodeOfStandard20_IsInTheSeverityTable()
    {
        var standards = Layouts().Select(Standard).ToList();
        var codes = standards.SelectMany(s => s.Checks.SelectMany(c => c.Codes))
            .Concat(standards.SelectMany(s => s.Descriptors.Select(d => d.Code)))
            .Concat(standards.SelectMany(s => s.LegacyStatuses.Keys))
            .ToHashSet();

        Assert.Empty(codes.Except(Expected.Keys));
        Assert.Empty(Expected.Keys.Except(codes));
    }

    [Fact]
    public void DescriptorsAndLegacyStatuses_GiveSeverityFromTheTable()
    {
        foreach (var standard in Layouts().Select(Standard))
        {
            Assert.All(standard.Descriptors, d =>
            {
                Assert.Equal(SpecStatuses.Severity(d.Status), d.Severity);
                Assert.Equal([d.Severity], Expected[d.Code]);
                Assert.False(string.IsNullOrWhiteSpace(d.SpecRef), d.Code + ": odwołanie do specyfikacji");
            });
            Assert.All(standard.LegacyStatuses, pair => Assert.Equal([SpecStatuses.Severity(pair.Value)], Expected[pair.Key]));
        }
    }

    [Fact]
    public void RequiredRequirements_RaisedForStandard20_AreErrors()
    {
        var statuses = Standard().LegacyStatuses;
        Assert.All(
            new[]
            {
                IssueCodes.PageFormatNotA4, IssueCodes.MarginsMissing, IssueCodes.InvalidMargins, IssueCodes.HeadingLevelSkipped,
                IssueCodes.VariantSlashAlternative, IssueCodes.TitleMissing, IssueCodes.AuthorMissing, IssueCodes.FooterTitleMissing,
            },
            code => Assert.Equal(SpecStatus.Required, statuses[code]));

        // „audit20-gaps” wywołuje wszystkie podniesione kody poza brakiem marginesów; ten daje dokument bez w:pgMar.
        var withoutMargins = Audit20.Validate(DocxBuilder.Compliant20().Heading(1, "A").Build20(main =>
            main.Document.Body!.Descendants<PageMargin>().Single().Remove()));
        var issues = Audit20.Gaps.Issues.Concat(withoutMargins.Issues).ToList();

        Assert.All(statuses, pair =>
        {
            var raised = issues.Where(i => i.Code == pair.Key).ToList();
            Assert.True(raised.Count > 0, $"Żaden dokument testowy nie wywołuje {pair.Key}.");
            Assert.All(raised, i => Assert.Equal((pair.Value, IssueSeverity.Error), (i.Status, i.Severity)));
        });
        Assert.False(Audit20.Gaps.IsValid);
    }

    [Fact]
    public void RecommendedExceptions_StayWarnings_G02_G16_TableTitle()
    {
        var document = DocxBuilder.Compliant20().Heading(1, "A")
            .Runs(new Run(new RunProperties(new Color { Val = "00AA00" }), new Text("kolor spoza palety")))
            .Runs(new Run(new RunProperties(new Color { Val = "7724FF" }), new Text("fiolet poza łączem")))
            .Paragraph("WZÓR")
            .Paragraph("Tabela")
            .Add(DocxBuilder.DataTable20(t => t.GetFirstChild<TableProperties>()!.GetFirstChild<TableCaption>()!.Remove()))
            .Build20();

        var result = Audit20.Validate(document);

        foreach (var code in new[] { IssueCodes.TextColorNotAllowed, IssueCodes.TextColorUsageMismatch, IssueCodes.SampleMarkNotOnEveryPage, IssueCodes.TableTitleMissing })
        {
            var issue = Assert.Single(result.Of(code));
            Assert.Equal((IssueSeverity.Warning, (SpecStatus?)null), (issue.Severity, issue.Status));
        }

        Assert.Empty(Standard().LegacyStatuses.Keys.Intersect([IssueCodes.TextColorNotAllowed, IssueCodes.TextColorUsageMismatch, IssueCodes.SampleMarkNotOnEveryPage, IssueCodes.TableTitleMissing]));
    }

    // Każde zgłoszenie standardu 2.0 na wszystkich dokumentach testowych (fixtury i pliki z samples/) ma wagę z tabeli.
    [Fact]
    public void EveryIssueOfStandard20_OnAllTestDocuments_HasSeverityFromTheTable()
    {
        var seen = new HashSet<string>();
        foreach (var name in SnapshotCases.Documents)
        {
            var bytes = SnapshotCases.Load(name);
            foreach (var layout in Layouts())
            {
                var result = Audit20.Validate(bytes, new CorporateStandard20Options { Layout = layout });
                Assert.All(result.Issues, i =>
                {
                    Assert.True(Expected.TryGetValue(i.Code, out var allowed), $"{name}: kod {i.Code} spoza tabeli.");
                    Assert.Contains(i.Severity, allowed!);
                    if (i.Status is { } status)
                    {
                        Assert.Equal(SpecStatuses.Severity(status), i.Severity);
                    }

                    seen.Add(i.Code);
                });
            }
        }

        Assert.True(seen.Count >= 30, $"Dokumenty testowe wywołały tylko {seen.Count} kodów.");
    }

    [Fact]
    public void OtherStandards_KeepTheirSeverities_AndBothEntriesStayWhenTheyDiffer()
    {
        var gaps = FixtureCatalog.Build("audit20-gaps");
        WcagValidationResult Run(params string[] standards) => new WcagDocxValidator().Validate(gaps, new WcagValidationProfile().WithStandards(standards));

        var v19 = Run("firmowy/1.9");
        Assert.NotEmpty(v19.Of(IssueCodes.InvalidMargins));
        Assert.All(v19.Of(IssueCodes.InvalidMargins).Concat(v19.Of(IssueCodes.PageFormatNotA4)), i => Assert.Equal((IssueSeverity.Warning, (SpecStatus?)null), (i.Severity, i.Status)));

        var wcag = Run("wcag/2.1");
        var skipped = Assert.Single(wcag.Of(IssueCodes.HeadingLevelSkipped));
        Assert.Equal((IssueSeverity.Warning, (SpecStatus?)null), (skipped.Severity, skipped.Status));
        Assert.Equal(IssueSeverity.Warning, Assert.Single(wcag.Of(IssueCodes.AuthorMissing)).Severity);

        // Ostrzeżenie WCAG nie połyka błędu 2.0: przy różnych wagach zostają oba wpisy; przy tej samej (brak tytułu) jeden.
        var both = Run("wcag/2.1", "firmowy/2.0");
        Assert.Equal(
            [("firmowy/2.0", IssueSeverity.Error), ("wcag/2.1", IssueSeverity.Warning)],
            both.Of(IssueCodes.HeadingLevelSkipped).Select(i => (i.Standard!, i.Severity)));
        Assert.Equal(
            [("firmowy/2.0", IssueSeverity.Error), ("wcag/2.1", IssueSeverity.Warning)],
            both.Of(IssueCodes.AuthorMissing).Select(i => (i.Standard!, i.Severity)));
        Assert.Equal("wcag/2.1", Assert.Single(both.Of(IssueCodes.TitleMissing)).Standard);
    }
}
