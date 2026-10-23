using System.Text;
using D2.Wcag;
using D2.Wcag.Docx;
using D2.Wcag.Standards;

// Sprawdzenie jednego pliku z wiersza poleceń, z opcjonalną naprawą do pliku „<nazwa>_poprawiony.docx”.
//   dotnet run --project samples/D2.Wcag.Console -- --list-standards
//   dotnet run --project samples/D2.Wcag.Console -- samples/przyklad.docx
//   dotnet run --project samples/D2.Wcag.Console -- samples/przyklad.docx --fix --standard wcag/2.1 --standard firmowy/2.5
Console.OutputEncoding = Encoding.UTF8;

string? path = null;
var fix = false;
var standards = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--fix":
            fix = true;
            break;
        case "--standard" when i + 1 < args.Length:
            standards.Add(args[++i]);
            break;
        case "--list-standards":
            foreach (var standard in StandardCatalog.Default.All)
            {
                Console.WriteLine($"{standard.Key(),-14} {standard.Name}{(standard.Confirmed ? "" : " (wersja robocza)")}");
            }

            return 0;
        default:
            path = args[i];
            break;
    }
}

if (path is null || !File.Exists(path))
{
    Console.Error.WriteLine("Użycie: D2.Wcag.Console <plik.docx> [--fix] [--standard id/wersja]... | --list-standards");
    Console.Error.WriteLine($"Domyślne standardy: {string.Join(", ", WcagValidationProfile.DefaultStandards)}");
    return 2;
}

var profile = standards.Count == 0 ? WcagValidationProfile.Default : WcagValidationProfile.Default.WithStandards([.. standards]);
WcagValidationResult result;
try
{
    result = new WcagDocxValidator(profile).ValidateFile(path);
}
catch (StandardNotFoundException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

Print(result, Path.GetFileName(path));

if (!fix || !result.IsDocumentReadable)
{
    return result.IsValid ? 0 : 1;
}

var output = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetFileNameWithoutExtension(path) + "_poprawiony" + Path.GetExtension(path));
var fixed_ = new WcagDocxFixer(profile).FixFile(path, output);

Console.WriteLine();
Console.WriteLine($"=== Naprawa → {output}");
if (!fixed_.Changed)
{
    Console.WriteLine("Nic do automatycznej naprawy.");
}

foreach (var applied in fixed_.Applied)
{
    Console.WriteLine($"  [{applied.Code}] {applied.Description}");
}

Console.WriteLine();
Print(fixed_.After, Path.GetFileName(output));
return fixed_.After.IsValid ? 0 : 1;

static void Print(WcagValidationResult result, string name)
{
    Console.WriteLine($"=== {name}: {(result.IsValid ? "POPRAWNY" : "NIEPOPRAWNY")} — {result.Summary}");
    Console.WriteLine($"Standardy: {string.Join("; ", result.Standards.Select(s => s.Name + (s.Confirmed ? "" : " (wersja robocza)")))}");
    if (result.Document is { } document)
    {
        Console.WriteLine($"Tytuł: {document.Title ?? "(brak)"} | Autor: {document.Author ?? "(brak)"} | Język: {document.Language ?? "(brak)"} | "
            + $"akapity: {document.Paragraphs}, nagłówki: {document.Headings}, tabele: {document.Tables}, obrazy: {document.Images}");
    }

    foreach (var group in result.Issues.GroupBy(i => i.Severity).OrderByDescending(g => g.Key))
    {
        Console.WriteLine();
        Console.WriteLine(group.Key switch { IssueSeverity.Error => "Błędy:", IssueSeverity.Warning => "Ostrzeżenia:", _ => "Informacje:" });
        foreach (var issue in group)
        {
            Console.WriteLine($"  [{issue.Code}] ({issue.Standard}) {issue.Message}");
        }
    }

    if (result.Outline.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Struktura nagłówków:");
        foreach (var entry in result.Outline)
        {
            Console.WriteLine($"  {new string(' ', (entry.Level - 1) * 2)}H{entry.Level} {entry.Text}");
        }
    }
}
