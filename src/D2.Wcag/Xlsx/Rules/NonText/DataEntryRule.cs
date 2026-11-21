using System.Globalization;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// Pola do wypełnienia. Wszystkie trzy kody to heurystyki „do weryfikacji” (macierz rozdz. 3.4 i 4):
/// <list type="bullet">
/// <item><c>XL-DV-INPUT</c> (3.3.2) – walidacja danych bez komunikatu wejściowego (brak, wyłączony albo pusty) <b>i</b> bez etykiety
/// w sąsiedniej komórce. Komunikat wejściowy to tylko jeden ze sposobów spełnienia 3.3.2, więc pole z etykietą nie jest zgłaszane.</item>
/// <item><c>XL-DV-ERROR</c> (3.3.3, nie 3.3.1) – walidacja z włączonym alertem o błędzie, ale bez własnej treści komunikatu: Excel pokaże
/// komunikat ogólny bez wskazówki, jak poprawić wartość. Tylko dla typów o znanym zbiorze poprawnych wartości. Walidacja z wyłączonym
/// alertem nie jest zgłaszana – Excel przyjmuje wtedy każdą wartość, więc błąd nie jest wykrywany i 3.3.3 nie ma zastosowania.</item>
/// <item><c>XL-FIELD-LABEL</c> (3.3.2) – odblokowana komórka chronionego arkusza bez walidacji danych i bez etykiety po lewej ani powyżej.
/// Komórek z walidacją ta reguła nie dubluje: bez komunikatu wejściowego zgłasza je <c>XL-DV-INPUT</c>, a z komunikatem mają instrukcję.</item>
/// </list>
/// Etykieta: niepusty tekst (nie liczba) w komórce bezpośrednio po lewej albo nad polem – przy ciągu pól liczy się komórka przed
/// pierwszym z nich; nagłówek kolumny Tabeli Excela też jest etykietą. W chronionym arkuszu etykietą nie jest komórka odblokowana.
/// </summary>
internal sealed class DataEntryRule : IXlsxRule
{
    public const string InputCode = "XL-DV-INPUT";
    public const string ErrorCode = "XL-DV-ERROR";
    public const string FieldCode = "XL-FIELD-LABEL";

    private const string InputFix =
        "1. Zaznacz komórki z regułą poprawności. 2. Wybierz Dane → Poprawność danych i przejdź na kartę Komunikat wejściowy. "
        + "3. Zaznacz „Pokazuj komunikat wejściowy przy wyborze komórki”, wpisz Tytuł i Komunikat wejściowy: co wpisać i w jakim formacie "
        + "(np. „Wpisz liczbę dni od 1 do 26”). Zamiast tego możesz wpisać etykietę w komórce bezpośrednio po lewej albo nad polem – "
        + "komunikat wejściowy nie jest jedynym sposobem opisania pola.";

    private const string ErrorFix =
        "1. Zaznacz komórki z regułą poprawności. 2. Wybierz Dane → Poprawność danych i przejdź na kartę Alert o błędzie. "
        + "3. Wpisz Tytuł i Komunikat o błędzie, który mówi, jak poprawić wartość: jakie wartości, zakres albo format są dozwolone "
        + "(np. „Wpisz liczbę całkowitą od 1 do 26”). Bez własnej treści Excel pokazuje komunikat ogólny, bez wskazówki.";

    private const string FieldFix =
        "1. Zdejmij ochronę arkusza: Recenzja → Nie chroń arkusza. 2. W komórce bezpośrednio po lewej albo nad polem wpisz etykietę mówiącą, "
        + "co należy wpisać (np. „Data rozpoczęcia (DD.MM.RRRR)”). 3. Opcjonalnie dodaj instrukcję: Dane → Poprawność danych → Komunikat wejściowy. "
        + "4. Włącz ochronę ponownie: Recenzja → Chroń arkusz.";

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(InputCode, "Pole z walidacją danych bez komunikatu wejściowego i etykiety", [XlsxWcagCriteria.Ref("3.3.2")], XlsxCheckType.Heuristic, InputFix)
        {
            Scope = "komunikaty wejściowe i etykiety pól z walidacją danych",
            DefaultConfidence = NonTextOptions.DvInputConfidence,
            HasSubject = w => w.Sheets.Any(s => s.DataValidations.Any(v => (v.Type ?? "none") != "none")),
        },
        new(ErrorCode, "Walidacja danych bez własnego komunikatu o błędzie", [XlsxWcagCriteria.Ref("3.3.3")], XlsxCheckType.Heuristic, ErrorFix)
        {
            Scope = "obecność własnych komunikatów o błędzie w walidacji danych o znanym zbiorze wartości",
            DefaultConfidence = NonTextOptions.DvErrorConfidence,
            HasSubject = w => w.Sheets.Any(s => s.DataValidations.Any(v =>
                v.ShowErrorMessage && NonTextOptions.DvTypesWithKnownValues.Contains(v.Type ?? "none", StringComparer.Ordinal))),
        },
        new(FieldCode, "Pole do wypełnienia bez etykiety", [XlsxWcagCriteria.Ref("3.3.2")], XlsxCheckType.Heuristic, FieldFix)
        {
            Scope = "etykiety komórek do wypełnienia w chronionych arkuszach",
            DefaultConfidence = NonTextOptions.FieldLabelConfidence,
            HasSubject = w => w.Sheets.Any(s => s.IsProtected),
        },
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            context.ThrowIfCancellationRequested();
            foreach (var validation in sheet.DataValidations)
            {
                var type = validation.Type ?? "none";
                if (type == "none")
                {
                    // Reguła „dowolna wartość” niczego nie ogranicza; służy co najwyżej do pokazania komunikatu wejściowego.
                    continue;
                }

                var hasPrompt = validation.ShowInputMessage
                    && (!NonTextOptions.IsBlank(validation.Prompt) || !NonTextOptions.IsBlank(validation.PromptTitle));
                if (!hasPrompt)
                {
                    var state = !NonTextOptions.IsBlank(validation.Prompt) || !NonTextOptions.IsBlank(validation.PromptTitle)
                        ? "wyłączony (treść jest wpisana, ale pokazywanie komunikatu nie jest zaznaczone)"
                        : validation.ShowInputMessage ? "pusty" : "brak";
                    foreach (var range in validation.Ranges)
                    {
                        if (RangeLabelled(workbook, sheet, range))
                        {
                            continue;
                        }

                        yield return new XlsxRuleHit(InputCode,
                            $"Komórki {range} mają regułę poprawności danych bez komunikatu wejściowego i bez etykiety w sąsiedniej komórce – sprawdź, czy użytkownik wie, co i w jakim formacie wpisać.",
                            XlsxLocation.OfRange(sheet, range))
                        {
                            Weight = (int)Math.Min(int.MaxValue, range.Cells),
                            Evidence =
                            [
                                new("Typ walidacji", TypeLabel(type)),
                                new("Komunikat wejściowy", state),
                                new("Etykieta po lewej / powyżej", "(brak)"),
                            ],
                        };
                    }
                }

                if (validation.ShowErrorMessage && NonTextOptions.IsBlank(validation.Error)
                    && NonTextOptions.DvTypesWithKnownValues.Contains(type, StringComparer.Ordinal))
                {
                    var evidence = new List<XlsxEvidence>
                    {
                        new("Typ walidacji", TypeLabel(type)),
                        new("Komunikat o błędzie", "(brak własnej treści – Excel pokaże komunikat ogólny)"),
                        new("Zakres", Ranges(validation.Ranges)),
                    };
                    if (!NonTextOptions.IsBlank(validation.ErrorTitle))
                    {
                        evidence.Add(new("Tytuł alertu", validation.ErrorTitle!));
                    }

                    if (!NonTextOptions.IsBlank(validation.Formula1))
                    {
                        evidence.Add(new("Warunek", XlsxLocation.Shorten(validation.Formula1)));
                    }

                    yield return new XlsxRuleHit(ErrorCode,
                        $"Reguła poprawności danych ({TypeLabel(type)}) nie ma własnego komunikatu o błędzie – po wpisaniu błędnej wartości użytkownik zobaczy ogólny komunikat Excela, bez informacji, jakie wartości są dozwolone.",
                        XlsxLocation.OfRange(sheet, validation.Ranges[0]))
                    {
                        Confidence = type == "list" ? NonTextOptions.DvErrorListConfidence : NonTextOptions.DvErrorConfidence,
                        Weight = (int)Math.Min(int.MaxValue, validation.Ranges.Sum(r => r.Cells)),
                        Evidence = evidence,
                    };
                }
            }

            foreach (var hit in UnlabelledFields(workbook, sheet, context))
            {
                yield return hit;
            }
        }
    }

    /// <summary>
    /// To, czy etykiety i instrukcje wystarczają oraz czy komunikaty podają sposób poprawy, ocenia człowiek – pytania zostają, gdy
    /// skoroszyt ma pola do wypełnienia (3.3.2) albo reguły poprawności danych (3.3.3).
    /// </summary>
    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        if (workbook.Sheets.Any(s => s.DataValidations.Count > 0 || s.Controls.Count > 0 || s.IsProtected))
        {
            yield return NonTextRules.CriterionCheck("3.3.2");
        }

        if (workbook.Sheets.Any(s => s.DataValidations.Count > 0))
        {
            yield return NonTextRules.CriterionCheck("3.3.3");
        }
    }

    private static IEnumerable<XlsxRuleHit> UnlabelledFields(XlsxWorkbook workbook, XlsxSheet sheet, XlsxAuditContext context)
    {
        if (!sheet.IsProtected)
        {
            yield break;
        }

        var fields = new HashSet<CellRef>();
        var counter = 0;
        foreach (var cell in sheet.Cells)
        {
            if ((++counter & 0xFFF) == 0)
            {
                context.ThrowIfCancellationRequested();
            }

            if (workbook.Styles.Format(cell.StyleIndex).Locked)
            {
                continue;
            }

            fields.Add(cell.Ref);
            if (fields.Count > NonTextOptions.MaxFieldCellsPerSheet)
            {
                // Odblokowana większość arkusza nie wyznacza pól formularza – zostaje pytanie checklisty 3.3.2.
                yield break;
            }
        }

        var unlabelled = new List<CellRef>();
        foreach (var field in fields)
        {
            if (sheet.DataValidations.Any(v => v.Ranges.Any(r => r.Contains(field))) || InTableWithHeader(sheet, field))
            {
                continue;
            }

            if (!HasLabel(workbook, sheet, fields, field, 0, -1) && !HasLabel(workbook, sheet, fields, field, -1, 0))
            {
                unlabelled.Add(field);
            }
        }

        foreach (var range in XlsxRanges.Group(unlabelled))
        {
            var what = range.IsSingleCell ? $"Komórka do wypełnienia {range}" : $"Komórki do wypełnienia {range}";
            yield return new XlsxRuleHit(FieldCode,
                $"{what} (bez blokady w chronionym arkuszu) – brak etykiety w sąsiedniej komórce po lewej i powyżej. Sprawdź, czy użytkownik wie, co wpisać.",
                XlsxLocation.OfRange(sheet, range))
            {
                Weight = (int)Math.Min(int.MaxValue, range.Cells),
                Evidence =
                [
                    new("Rodzaj pola", "komórka odblokowana w chronionym arkuszu"),
                    new("Etykieta po lewej / powyżej", "(brak)"),
                    new("Walidacja danych", "(brak)"),
                ],
            };
        }
    }

    /// <summary>Idzie od pola w podanym kierunku przez sąsiednie pola; pierwsza komórka niebędąca polem musi być etykietą.</summary>
    private static bool HasLabel(XlsxWorkbook workbook, XlsxSheet sheet, HashSet<CellRef> fields, CellRef field, int rowStep, int columnStep)
    {
        var row = field.Row;
        var column = field.Column;
        for (var step = 0; step < NonTextOptions.LabelProbeLimit; step++)
        {
            row += rowStep;
            column += columnStep;
            if (row < 1 || column < 1)
            {
                return false;
            }

            var current = new CellRef(row, column);
            if (!fields.Contains(current))
            {
                return IsLabel(workbook, sheet, current);
            }
        }

        return false;
    }

    private static bool RangeLabelled(XlsxWorkbook workbook, XlsxSheet sheet, RangeRef range)
    {
        if (InTableWithHeader(sheet, range.From))
        {
            return true;
        }

        // Walidacje na całe kolumny lub wiersze: etykiet szukamy tylko w zasięgu obszaru z treścią.
        var used = sheet.UsedRange;
        var lastRow = Math.Min(range.To.Row, Math.Max(range.From.Row, used?.To.Row ?? range.From.Row));
        var lastColumn = Math.Min(range.To.Column, Math.Max(range.From.Column, used?.To.Column ?? range.From.Column));
        lastRow = Math.Min(lastRow, range.From.Row + NonTextOptions.LabelProbeLimit - 1);
        lastColumn = Math.Min(lastColumn, range.From.Column + NonTextOptions.LabelProbeLimit - 1);

        var above = range.From.Row > 1;
        for (var column = range.From.Column; above && column <= lastColumn; column++)
        {
            above = IsLabel(workbook, sheet, new CellRef(range.From.Row - 1, column));
        }

        if (above)
        {
            return true;
        }

        var left = range.From.Column > 1;
        for (var row = range.From.Row; left && row <= lastRow; row++)
        {
            left = IsLabel(workbook, sheet, new CellRef(row, range.From.Column - 1));
        }

        return left;
    }

    private static bool InTableWithHeader(XlsxSheet sheet, CellRef cell) =>
        sheet.Tables.Any(t => t.HeaderRowCount > 0 && t.Ref.Contains(cell) && cell.Row > t.Ref.From.Row);

    private static bool IsLabel(XlsxWorkbook workbook, XlsxSheet sheet, CellRef reference)
    {
        // Tekst scalonej komórki jest zapisany w jej lewym górnym rogu.
        foreach (var merged in sheet.MergedRanges)
        {
            if (merged.Contains(reference))
            {
                reference = merged.From;
                break;
            }
        }

        if (sheet.Cell(reference) is not { } cell || !NonTextOptions.IsLabelText(cell.Text))
        {
            return false;
        }

        return !sheet.IsProtected || workbook.Styles.Format(cell.StyleIndex).Locked;
    }

    private static string Ranges(IReadOnlyList<RangeRef> ranges)
    {
        const int shown = 5;
        var text = string.Join(", ", ranges.Take(shown).Select(r => r.ToString()));
        return ranges.Count > shown ? text + string.Create(CultureInfo.InvariantCulture, $" (+{ranges.Count - shown})") : text;
    }

    private static string TypeLabel(string type) => type switch
    {
        "list" => "lista",
        "whole" => "liczba całkowita",
        "decimal" => "liczba dziesiętna",
        "date" => "data",
        "time" => "godzina",
        "textLength" => "długość tekstu",
        "custom" => "formuła niestandardowa",
        _ => type,
    };
}
