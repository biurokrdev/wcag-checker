using D2.Wcag.Docx;
using D2.Wcag.Rules;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Fixes;

// Naprawy standardu 2.0, tylko bezpieczne i jednoznaczne: wyrównanie do lewej, wiersz nagłówkowy i niepodzielne
// wiersze tabel danych, tytuł z dopiskiem banku i autor = bank. Typografii, kolorów i treści wariantów nie zmieniamy
// automatycznie. Każda naprawa dotyka tylko tego, co zgłasza odpowiadająca jej reguła (te same warunki).

/// <summary>Akapity z tekstem, które są wyjustowane, dostają wyrównanie do lewej (bezpośrednio w akapicie, styl zostaje).</summary>
internal sealed class Justification20Fix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.JustificationForbidden];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var changed = 0;
        foreach (var paragraph in document.Paragraphs.ToList())
        {
            // Jak w regule G-04: pusty akapit nie jest zgłaszany, więc naprawa go nie rusza.
            if (paragraph.InnerText.Trim().Length == 0 || !document.ResolveParagraph(paragraph).Justified)
            {
                continue;
            }

            paragraph.ParagraphProperties ??= new ParagraphProperties();
            paragraph.ParagraphProperties.Justification = new Justification { Val = JustificationValues.Left };
            changed++;
        }

        if (changed > 0)
        {
            yield return new AppliedFix(IssueCodes.JustificationForbidden, $"Wyrównano do lewej {Polish.Count(changed, "akapit", "akapity", "akapitów")}.");
        }
    }
}

/// <summary>
/// Pierwszy wiersz tabeli danych zostaje oznaczony jako nagłówkowy (w:tblHeader). Jak reguła G-08, pomija tabele
/// układu (jeden wiersz albo jedna kolumna), których standard nie ocenia; wiersz z pustą komórką zostaje dla człowieka.
/// </summary>
internal sealed class TableHeader20Fix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.TableMissingHeaders];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var changed = 0;
        foreach (var table in document.Tables.ToList())
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0 || TableRules.IsLayoutTable(rows) || TableRules.HasHeaderRow(rows[0])
                || rows[0].Elements<TableCell>().Any(c => string.IsNullOrWhiteSpace(c.InnerText)))
            {
                continue;
            }

            rows[0].TableRowProperties ??= new TableRowProperties();
            rows[0].TableRowProperties!.Append(new TableHeader());
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

/// <summary>
/// Wiersz tabeli danych, który może dzielić się między strony, dostaje „Nie dziel wiersza między stronami”
/// (w:cantSplit). Wiersz, któremu daje to styl tabeli, jest już poprawny (jak w regule) i zostaje bez zmian.
/// </summary>
internal sealed class TableRows20Fix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.TableRowCanSplit];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var changed = 0;
        foreach (var table in document.Tables.ToList())
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0 || TableRules.IsLayoutTable(rows))
            {
                continue;
            }

            foreach (var row in rows)
            {
                if (TableCells20.CantSplit(row, document))
                {
                    continue;
                }

                row.TableRowProperties ??= new TableRowProperties();
                row.TableRowProperties.GetFirstChild<CantSplit>()?.Remove();
                row.TableRowProperties.PrependChild(new CantSplit());
                changed++;
            }
        }

        if (changed > 0)
        {
            yield return new AppliedFix(IssueCodes.TableRowCanSplit, $"Zablokowano dzielenie między strony dla {Polish.Count(changed, "wiersza", "wierszy", "wierszy")} tabel.");
        }
    }
}

/// <summary>Tytuł: „[Tytuł]. {instytucja}” (dopisek po kropce); autor zastępowany nazwą banku.</summary>
internal sealed class Metadata20Fix(string institution) : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } =
        [IssueCodes.TitleMissing, IssueCodes.TitleSuffixInvalid, IssueCodes.AuthorMissing, IssueCodes.AuthorNotInstitution];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var properties = document.Package.PackageProperties;
        var suffix = ". " + institution;

        if (!string.IsNullOrWhiteSpace(properties.Title))
        {
            var title = properties.Title.Trim();
            if (!title.EndsWith(suffix, StringComparison.Ordinal))
            {
                var core = title.Replace(institution, "", StringComparison.OrdinalIgnoreCase).Trim().TrimEnd('.', ' ', '-', '–');
                if (core.Length > 0)
                {
                    properties.Title = core + suffix;
                    yield return new AppliedFix(IssueCodes.TitleSuffixInvalid, $"Ustawiono tytuł „{properties.Title}”.");
                }
            }
        }

        if (!string.Equals(properties.Creator?.Trim(), institution, StringComparison.Ordinal))
        {
            properties.Creator = institution;
            yield return new AppliedFix(IssueCodes.AuthorNotInstitution, $"Autorem ustawiono „{institution}”.");
        }
    }
}
