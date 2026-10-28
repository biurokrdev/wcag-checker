using System.Text;
using D2.Wcag;
using D2.Wcag.Docx;
using D2.Wcag.Standards;
using D2.Wcag.TemplateGenerator;

// Generuje wzorcowy szablon pisma zgodny z WCAG 2.1 i standardem pism firmowych 2.0, a potem sprawdza go oboma
// standardami. Wynik bez błędów i ostrzeżeń jest warunkiem zapisu (kod wyjścia 1, gdy szablon nie przechodzi).
//   dotnet run --project samples/TemplateGenerator -- samples/szablon_pisma_2.0.docx
Console.OutputEncoding = Encoding.UTF8;

var output = args.Length > 0 ? args[0] : Path.Combine("samples", "szablon_pisma_2.0.docx");
var document = LetterTemplate.Build();

var profile = new WcagValidationProfile().WithStandards(WcagStandard.StandardKey, "firmowy/2.0");
var result = new WcagDocxValidator().Validate(document, profile);
var problems = result.Issues.Where(i => i.Severity != IssueSeverity.Info).ToList();

Console.WriteLine($"Szablon: {result.Summary}");
foreach (var issue in result.Issues)
{
    Console.WriteLine($"  [{issue.Severity}] {issue.Code} ({issue.Standard}): {issue.Message}");
}

if (problems.Count > 0)
{
    Console.Error.WriteLine("Szablon nie przechodzi walidacji; plik nie został zapisany.");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllBytes(output, document);
Console.WriteLine($"Zapisano {Path.GetFullPath(output)}");
return 0;
