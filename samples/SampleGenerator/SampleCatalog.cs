namespace D2.Wcag.SampleGenerator;

public enum SampleKind
{
    Docx,
    Xlsx,
}

/// <summary>
/// Profil sprawdzenia: zestaw standardów DOCX albo wersja WCAG dla XLSX, z kodami zgłoszeń, które MUSZĄ wystąpić
/// (w dowolnej wadze). Zgłoszeń o wadze błąd, ostrzeżenie albo do weryfikacji spoza listy nie może być; informacje
/// spoza listy są dozwolone i trafiają do manifestu.
/// </summary>
public sealed record SampleProfile(string Name, IReadOnlyList<string> Standards, string? WcagVersion, IReadOnlyList<string> ExpectedCodes);

public sealed record Sample(string FileName, SampleKind Kind, Func<byte[]> Build, IReadOnlyList<SampleProfile> Profiles)
{
    public string ManifestName => Path.GetFileNameWithoutExtension(FileName) + ".expected.json";
}

/// <summary>Cztery pliki przykładowe i ich oczekiwane wyniki (zamiar autora; manifest powstaje z prawdziwego walidatora).</summary>
public static class SampleCatalog
{
    public const string CompliantLetterFile = "przyklad_pismo_zgodne.docx";
    public const string NonCompliantLetterFile = "przyklad_pismo_niezgodne.docx";
    public const string CompliantWorkbookFile = "przyklad_arkusz_zgodny.xlsx";
    public const string NonCompliantWorkbookFile = "przyklad_arkusz_niezgodny.xlsx";

    private static SampleProfile Docx(string name, string[] standards, params string[] codes) => new(name, standards, null, codes);

    private static SampleProfile Xlsx(string version, params string[] codes) => new("WCAG " + version, [], version, codes);

    public static IReadOnlyList<Sample> All { get; } =
    [
        new(CompliantLetterFile, SampleKind.Docx, CompliantLetter.Build,
        [
            Docx("wcag/2.1", ["wcag/2.1"]),
            Docx("firmowy/2.0", ["firmowy/2.0"]),
            Docx("style/1.0", ["style/1.0"]),
            Docx("hierarchia/1.0", ["hierarchia/1.0"]),
            Docx("wcag/2.1 + firmowy/2.0 + style/1.0 + hierarchia/1.0", ["wcag/2.1", "firmowy/2.0", "style/1.0", "hierarchia/1.0"]),
            // Sprzeczność standardów: autor ma być dokładnie „Example Company S. A.” (2.0), a zawierać „SUN Example Company S. A.” (1.9).
            Docx("firmowy/1.9", ["firmowy/1.9"], "AUTHOR_PHRASE_MISSING"),
            Docx("wcag/2.1 + firmowy/1.9", ["wcag/2.1", "firmowy/1.9"], "AUTHOR_PHRASE_MISSING"),
        ]),
        new(NonCompliantLetterFile, SampleKind.Docx, NonCompliantLetter.Build, NonCompliantLetter.Profiles),
        new(CompliantWorkbookFile, SampleKind.Xlsx, () => BudgetWorkbook.Build(compliant: true),
        [
            Xlsx("2.2"),
            Xlsx("2.1"),
        ]),
        new(NonCompliantWorkbookFile, SampleKind.Xlsx, () => BudgetWorkbook.Build(compliant: false), BudgetWorkbook.NonCompliantProfiles),
    ];
}
