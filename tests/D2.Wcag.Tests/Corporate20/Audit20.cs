using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using D2.Wcag.Standards;

namespace D2.Wcag.Tests.Corporate20;

// Wspólne dla testów luk standardu firmowy/2.0: walidacja samym standardem 2.0 (także z wybranym układem dokumentu)
// oraz wyniki dla fixtur „audit20-gaps” (przypadki pozytywne i negatywne) i „clean” (zero zgłoszeń).
internal static class Audit20
{
    public const string Key = "firmowy/2.0";

    public static readonly WcagValidationProfile Only20 = new WcagValidationProfile().WithStandards(Key);

    private static readonly Lazy<WcagValidationResult> LazyGaps = new(() => Validate(FixtureCatalog.Build("audit20-gaps")));
    private static readonly Lazy<WcagValidationResult> LazyClean = new(() => Validate(FixtureCatalog.Build("clean")));

    /// <summary>Wynik standardu 2.0 bez wybranego układu dla fixtury „audit20-gaps”.</summary>
    public static WcagValidationResult Gaps => LazyGaps.Value;

    /// <summary>Wynik standardu 2.0 bez wybranego układu dla fixtury „clean”.</summary>
    public static WcagValidationResult Clean => LazyClean.Value;

    public static WcagValidationResult Validate(byte[] docx) => new WcagDocxValidator().Validate(docx, Only20);

    /// <summary>Walidacja standardem 2.0 z parametrami (układ dokumentu, style danych rejestrowych) podmienionym w katalogu.</summary>
    public static WcagValidationResult Validate(byte[] docx, CorporateStandard20Options options, params string[] standardsBefore)
    {
        var catalog = StandardCatalog.Default.With(new CorporateStandard20(options));
        return new WcagDocxValidator(catalog: catalog).Validate(docx, new WcagValidationProfile().WithStandards([.. standardsBefore, Key]));
    }

    public static WcagValidationResult Validate(byte[] docx, DocumentLayout20 layout) =>
        Validate(docx, new CorporateStandard20Options { Layout = layout });

    public static WcagValidationResult Fixture(string name, DocumentLayout20 layout) => Validate(FixtureCatalog.Build(name), layout);

    public static List<WcagIssue> Of(this WcagValidationResult result, string code) => result.Issues.Where(i => i.Code == code).ToList();

    /// <summary>Wszystkie wystąpienia zgłoszeń o danym kodzie.</summary>
    public static List<IssueLocation> LocationsOf(this WcagValidationResult result, string code) =>
        result.Issues.Where(i => i.Code == code).SelectMany(i => i.Locations).ToList();

    /// <summary>Nowa reguła: kod, status i waga z tabeli, tytuł, instrukcja naprawy krok po kroku.</summary>
    public static void AssertDescribed(WcagIssue issue, SpecStatus status)
    {
        Assert.Equal(status, issue.Status);
        Assert.Equal(SpecStatuses.Severity(status), issue.Severity);
        Assert.False(string.IsNullOrWhiteSpace(issue.Title), issue.Code + ": tytuł");
        Assert.StartsWith("1. ", issue.Fix);
        Assert.Contains("2. ", issue.Fix);
        Assert.DoesNotContain("{", issue.Fix);
        Assert.Equal(Key, issue.Standard);
    }
}
