using D2.Wcag.Docx;
using D2.Wcag.Rules;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Fixes;

// Pierwszy wiersz tabeli zostaje oznaczony jako wiersz nagłówkowy (w:tblHeader): czytnik ekranu podaje go
// przy każdej komórce, a Word powtarza go na kolejnych stronach.
internal sealed class TableHeaderFix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.TableMissingHeaders];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var changed = 0;
        foreach (var table in document.Tables.ToList())
        {
            // Wiersz z pustą komórką nie nadaje się na nagłówek (TABLE_HEADER_CELL_EMPTY); to zostaje dla człowieka.
            var first = table.Elements<TableRow>().FirstOrDefault();
            if (first is null || !first.Elements<TableCell>().Any() || TableRules.HasHeaderRow(first)
                || first.Elements<TableCell>().Any(c => string.IsNullOrWhiteSpace(c.InnerText)))
            {
                continue;
            }

            first.TableRowProperties ??= new TableRowProperties();
            first.TableRowProperties.Append(new TableHeader());
            changed++;
        }

        if (changed > 0)
        {
            yield return new AppliedFix(
                IssueCodes.TableMissingHeaders,
                $"Oznaczono pierwszy wiersz jako nagłówkowy w {Polish.Count(changed, "tabeli", "tabelach", "tabelach")}.");
        }
    }
}
