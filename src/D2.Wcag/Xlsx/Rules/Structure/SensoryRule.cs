using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// XL-SENSORY (1.3.3): teksty odwołujące się do koloru, położenia, kształtu albo rozmiaru („zaznaczone na czerwono”,
/// „po prawej”, „zielone komórki”). Wyszukiwanie zwrotów nie rozstrzyga, czy to instrukcja i czy informacja jest podana
/// także inaczej – wynik zawsze „do weryfikacji”, z potwierdzeniem ręcznym. Wzorce: <see cref="SensoryPatterns"/>.
/// <para>
/// Przeszukiwane są teksty komórek, notatki i komentarze, komunikaty walidacji danych oraz tekst kształtów i pól
/// tekstowych – na arkuszach widocznych. Komórki o tej samej treści łączone są w zakresy (jedno zgłoszenie na prostokąt).
/// </para>
/// </summary>
internal sealed class SensoryRule : IXlsxRule
{
    public const string Code = "XL-SENSORY";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Tekst odwołuje się do koloru, położenia lub kształtu",
        [XlsxWcagCriteria.Ref("1.3.3")],
        XlsxCheckType.Heuristic,
        "1. Przeczytaj wskazany tekst i sprawdź, czy da się go zrozumieć bez widzenia koloru, położenia i kształtu. "
        + "2. Jeśli nie, dopisz odniesienie niezależne od wyglądu: nazwę arkusza, tabeli lub kolumny, adres komórki albo treść etykiety, np. zamiast „pola zaznaczone na czerwono” – „pola z dopiskiem »Do poprawy« w kolumnie Status”, zamiast „tabela po prawej” – „tabela »Podsumowanie« (od komórki H2)”. "
        + "3. Informację przekazywaną kolorem powtórz tekstem lub znakiem w komórce.")
    {
        Scope = "teksty odwołujące się do koloru, położenia lub kształtu",
        DefaultConfidence = StructureOptions.SensoryConfidence,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        // Teksty wspólne to te same obiekty w wielu komórkach – wynik dopasowania pamiętamy po referencji, więc arkusz
        // z milionem powtórzeń kilku etykiet kosztuje kilka dopasowań.
        var cache = new Dictionary<string, IReadOnlyList<(string Kind, string Phrase)>>(ReferenceEqualityComparer.Instance);

        IReadOnlyList<(string Kind, string Phrase)> Find(string text)
        {
            if (text.Length < StructureOptions.SensoryMinTextLength)
            {
                return [];
            }

            if (!cache.TryGetValue(text, out var phrases))
            {
                cache[text] = phrases = SensoryPatterns.Find(text);
            }

            return phrases;
        }

        foreach (var sheet in workbook.Sheets)
        {
            if (sheet.State != XlsxSheetState.Visible)
            {
                continue;
            }

            context.ThrowIfCancellationRequested();

            // Komórki: grupowanie po treści, potem w zakresy.
            Dictionary<string, List<CellRef>>? byText = null;
            var cells = sheet.Cells;
            for (var i = 0; i < cells.Count; i++)
            {
                if ((i & StructureOptions.CancellationCheckMask) == 0)
                {
                    context.ThrowIfCancellationRequested();
                }

                var cell = cells[i];
                if (cell.Type != XlsxCellType.Text || !cell.HasValue)
                {
                    continue;
                }

                var text = cell.Text;
                if (Find(text).Count == 0)
                {
                    continue;
                }

                byText ??= new Dictionary<string, List<CellRef>>(StringComparer.Ordinal);
                if (!byText.TryGetValue(text, out var list))
                {
                    byText[text] = list = [];
                }

                list.Add(cell.Ref);
            }

            if (byText is not null)
            {
                foreach (var (text, references) in byText)
                {
                    var phrases = SensoryPatterns.Find(text);
                    foreach (var range in XlsxRanges.Group(references))
                    {
                        yield return Hit(XlsxLocation.OfRange(sheet, range, text), "Tekst komórki", phrases, (int)Math.Min(int.MaxValue, range.Cells));
                    }
                }
            }

            foreach (var comment in sheet.Comments)
            {
                var phrases = Find(comment.Text);
                if (phrases.Count > 0)
                {
                    yield return Hit(XlsxLocation.OfCell(sheet, comment.Ref, comment.Text), comment.Threaded ? "Komentarz" : "Notatka", phrases, 1);
                }
            }

            foreach (var validation in sheet.DataValidations)
            {
                if (validation.Ranges.Count == 0)
                {
                    continue;
                }

                var texts = new[] { validation.PromptTitle, validation.Prompt, validation.ErrorTitle, validation.Error };
                var message = string.Join(" ", texts.Where(t => !string.IsNullOrWhiteSpace(t)));
                var phrases = SensoryPatterns.Find(message);
                if (phrases.Count > 0)
                {
                    yield return Hit(XlsxLocation.OfRange(sheet, validation.Ranges[0], message), "Komunikat walidacji danych", phrases, 1);
                }
            }

            foreach (var drawing in sheet.Drawings)
            {
                var phrases = Find(drawing.Text ?? "");
                if (phrases.Count > 0 && !drawing.Hidden)
                {
                    yield return Hit(XlsxLocation.OfDrawing(sheet, drawing) with { Snippet = XlsxLocation.Shorten(drawing.Text) }, "Tekst kształtu", phrases, 1);
                }
            }
        }
    }

    private static XlsxRuleHit Hit(XlsxLocation location, string source, IReadOnlyList<(string Kind, string Phrase)> phrases, int weight)
    {
        var shown = phrases.Take(StructureOptions.MaxSensoryPhrasesInEvidence).Select(p => "„" + p.Phrase + "”").ToList();
        var kinds = string.Join(", ", phrases.Select(p => p.Kind).Distinct(StringComparer.Ordinal));
        return new XlsxRuleHit(Code,
            $"{source} odwołuje się do właściwości zmysłowych ({kinds}): {string.Join(", ", shown)}. Sprawdź, czy treść jest zrozumiała bez widzenia koloru, położenia i kształtu – możliwy problem z kryterium 1.3.3, wymaga weryfikacji przez człowieka.",
            location)
        {
            Evidence =
            [
                new XlsxEvidence("Źródło tekstu", source),
                new XlsxEvidence("Znalezione zwroty", string.Join(", ", shown)),
                new XlsxEvidence("Rodzaj odwołania", kinds),
            ],
            Weight = weight,
        };
    }
}
