using D2.Wcag.Docx;
using D2.Wcag.Rules;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Fixes;

// Naprawy standardu 2.0, tylko bezpieczne i jednoznaczne: wyrównanie do lewej, niepodzielne wiersze tabel,
// tytuł z dopiskiem banku i autor = bank. Typografii, kolorów i treści wariantów nie zmieniamy automatycznie.

/// <summary>Akapity wyjustowane dostają wyrównanie do lewej (bezpośrednio w akapicie, styl zostaje).</summary>
internal sealed class Justification20Fix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.JustificationForbidden];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var changed = 0;
        foreach (var paragraph in document.Paragraphs.ToList())
        {
            if (!document.ResolveParagraph(paragraph).Justified)
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

/// <summary>Każdy wiersz tabeli danych dostaje „Nie dziel wiersza między stronami” (w:cantSplit).</summary>
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
                if (row.TableRowProperties?.GetFirstChild<CantSplit>() is { } existing && existing.Val?.Value != OnOffOnlyValues.Off)
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
