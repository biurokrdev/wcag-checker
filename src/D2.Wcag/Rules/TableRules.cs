using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// WCAG 1.3.1: tabela danych ma oznaczony wiersz nagłówkowy z tekstem w każdej komórce, regularną siatkę bez scaleń,
// podpis (akapit przed tabelą, styl Legenda za nią albo w:tblCaption) i nie zawiera tabel zagnieżdżonych.
// Tabela jednowierszowa albo jednokolumnowa to tabela układu: dostaje własne ostrzeżenie zamiast wymagań tabeli danych.
internal sealed class TableRules : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var index = 0;
        foreach (var table in document.Tables)
        {
            index++;
            var location = document.Locate(table);
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0 || rows.Any(r => !r.Elements<TableCell>().Any()))
            {
                yield return new WcagIssue(
                    IssueCodes.TableStructureInvalid, IssueSeverity.Error, IssueCategory.Tables,
                    $"Tabela {index} ma niepoprawną strukturę: brak wierszy albo wiersz bez komórek.").At(location);
                continue;
            }

            if (IsLayoutTable(rows))
            {
                yield return new WcagIssue(
                    IssueCodes.TableLayoutSuspected, IssueSeverity.Warning, IssueCategory.Tables,
                    $"Tabela {index} ma {(rows.Count == 1 ? "jeden wiersz" : "jedną kolumnę")}, więc nie przedstawia danych, tylko układ; czytnik ekranu i tak ogłosi tabelę. Dla ramki albo kolumn użyj obramowania akapitu albo kolumn sekcji.").At(location);
                continue;
            }

            if (!HasHeaderRow(rows[0]))
            {
                yield return MissingHeaderRow(index, location);
            }
            else
            {
                var emptyHeaders = rows[0].Elements<TableCell>().Where(c => string.IsNullOrWhiteSpace(c.InnerText)).ToList();
                if (emptyHeaders.Count > 0)
                {
                    yield return new WcagIssue(
                        IssueCodes.TableHeaderCellEmpty, IssueSeverity.Error, IssueCategory.Tables,
                        $"Tabela {index}: {Polish.Count(emptyHeaders.Count, "komórka wiersza nagłówkowego jest pusta", "komórki wiersza nagłówkowego są puste", "komórek wiersza nagłówkowego jest pustych")}; czytnik ekranu nie nazwie takiej kolumny.")
                        .At(emptyHeaders.Select(c => document.Locate(c, "(pusta komórka nagłówka)")));
                }
            }

            var widths = rows.Select(ColumnCount).Distinct().ToList();
            if (widths.Count > 1)
            {
                yield return new WcagIssue(
                    IssueCodes.TableIrregular, IssueSeverity.Warning, IssueCategory.Tables,
                    $"Tabela {index} ma różną liczbę kolumn w wierszach ({string.Join(", ", widths)}); czytnik ekranu może gubić kontekst komórek.").At(location);
            }

            var merged = rows.SelectMany(r => r.Elements<TableCell>()).Where(IsMerged).ToList();
            if (merged.Count > 0)
            {
                yield return new WcagIssue(
                    IssueCodes.TableMergedCells, IssueSeverity.Warning, IssueCategory.Tables,
                    $"Tabela {index} ma {Polish.Count(merged.Count, "scaloną komórkę", "scalone komórki", "scalonych komórek")}; prosta siatka wiersz × kolumna jest czytelniejsza dla czytnika ekranu.")
                    .At(merged.Select(c => document.Locate(c)));
            }

            if (!HasCaption(table, document))
            {
                yield return new WcagIssue(
                    IssueCodes.TableCaptionMissing, IssueSeverity.Warning, IssueCategory.Tables,
                    $"Tabela {index} nie ma podpisu (akapit przed tabelą, styl Legenda albo tytuł w Tekście alternatywnym tabeli).").At(location);
            }

            if (table.Descendants<Table>().Any())
            {
                yield return new WcagIssue(
                    IssueCodes.NestedTable, IssueSeverity.Warning, IssueCategory.Tables,
                    $"Tabela {index} zawiera zagnieżdżone tabele, co utrudnia logiczną kolejność odczytu.").At(location);
            }
        }
    }

    public static bool HasHeaderRow(TableRow row) => row.TableRowProperties?.Elements<TableHeader>().Any() == true;

    /// <summary>Brak wiersza nagłówkowego; wspólne dla WCAG i standardu firmowego 2.0 (G-08), walidator scala duplikaty.</summary>
    public static WcagIssue MissingHeaderRow(int index, IssueLocation? location) => new WcagIssue(
        IssueCodes.TableMissingHeaders, IssueSeverity.Error, IssueCategory.Tables,
        $"Tabela {index} nie ma oznaczonego wiersza nagłówkowego (Właściwości tabeli → Powtórz jako wiersz nagłówka).").At(location);

    /// <summary>Jeden wiersz albo jedna kolumna: nie ma relacji nagłówek–dane, to tabela układu.</summary>
    public static bool IsLayoutTable(IReadOnlyList<TableRow> rows) =>
        rows.Count == 1 || rows.All(r => r.Elements<TableCell>().Count() == 1);

    private static int ColumnCount(TableRow row) =>
        row.Elements<TableCell>().Sum(c => c.TableCellProperties?.GridSpan?.Val?.Value ?? 1);

    private static bool IsMerged(TableCell cell) =>
        cell.TableCellProperties?.GridSpan?.Val?.Value is > 1 || cell.TableCellProperties?.VerticalMerge is not null;

    // Podpis: tytuł w właściwościach tabeli, najbliższy niepusty akapit przed tabelą (pomijając puste, ale nie inną tabelę)
    // albo akapit w stylu Legenda tuż za nią.
    private static bool HasCaption(Table table, DocxDocument document)
    {
        var properties = table.GetFirstChild<TableProperties>();
        if (!string.IsNullOrWhiteSpace(properties?.GetFirstChild<TableCaption>()?.Val?.Value)
            || !string.IsNullOrWhiteSpace(properties?.GetFirstChild<TableDescription>()?.Val?.Value))
        {
            return true;
        }

        var before = table.ElementsBefore().OfType<Paragraph>().LastOrDefault(p => !string.IsNullOrWhiteSpace(p.InnerText));
        if (before is not null && !table.ElementsBefore().SkipWhile(e => e != before).Skip(1).OfType<Table>().Any())
        {
            return true;
        }

        var after = table.ElementsAfter().OfType<Paragraph>().FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.InnerText));
        return after is not null && document.IsCaptionStyle(after);
    }
}
