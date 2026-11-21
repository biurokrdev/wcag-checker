using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// <c>XL-TEXTBOX</c> (1.3.2, heurystyka): pole tekstowe albo kształt z treścią merytoryczną. Tekst w obiekcie pływającym leży poza
/// siatką komórek, więc jego miejsce w kolejności odczytu nie wynika z położenia na arkuszu. Macierz (rozdz. 4) dopuszcza 1.3.1
/// tylko wtedy, gdy pole pełni rolę nagłówka lub etykiety danych – tego z pliku nie ustalimy, więc reguła wiąże się wyłącznie z 1.3.2.
/// Pomijane: kontrolki i ich podpisy, obiekty ukryte, teksty krótsze niż <see cref="NonTextOptions.TextBoxMinLength"/> znaków
/// (etykiety, podpisy przycisków).
/// </summary>
internal sealed class TextBoxRule : IXlsxRule
{
    public const string Code = "XL-TEXTBOX";

    private const string Fix =
        "1. Jeśli to możliwe, przenieś tekst do komórek arkusza (np. do wiersza nad danymi) i usuń pole tekstowe – tekst w komórkach jest "
        + "czytany w przewidywalnej kolejności. 2. Jeśli pole musi zostać, umieść je tuż przy danych, których dotyczy, i sprawdź kolejność "
        + "obiektów na liście Układ strony → Okienko zaznaczenia. "
        + "3. Sprawdź z czytnikiem ekranu, czy tekst pola jest odczytywany i czy pada we właściwym miejscu.";

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(Code, "Treść w polu tekstowym poza siatką komórek", [XlsxWcagCriteria.Ref("1.3.2")], XlsxCheckType.Heuristic, Fix)
        {
            Scope = "treść pól tekstowych i kształtów leżącą poza siatką komórek",
            DefaultConfidence = NonTextOptions.TextBoxConfidence,
        },
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            foreach (var info in NonTextRules.Objects(sheet))
            {
                var obj = info.Object;
                if (info.Hidden || obj.IsGroup || obj.Type is not (XlsxDrawingType.TextBox or XlsxDrawingType.Shape))
                {
                    continue;
                }

                var text = obj.Text.Trim();
                if (text.Length < NonTextOptions.TextBoxMinLength)
                {
                    continue;
                }

                var kind = obj.Type == XlsxDrawingType.TextBox ? "Pole tekstowe" : "Kształt";
                yield return new XlsxRuleHit(Code,
                    $"{kind} „{obj.Name}” zawiera treść ({text.Length} znaków), która leży poza siatką komórek – sprawdź, czy czytnik ekranu odczytuje ją we właściwym miejscu.",
                    XlsxLocation.OfDrawing(sheet, obj))
                {
                    Evidence =
                    [
                        new("Tekst", XlsxLocation.Shorten(text)),
                        new("Liczba znaków", text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ],
                };
            }
        }
    }

    /// <summary>
    /// Reguła wyłapuje tylko dłuższe teksty w polach; to, czy obiekty pływające (obrazy, wykresy, pola, kontrolki) padają we
    /// właściwym miejscu kolejności odczytu, ocenia człowiek – pytanie zostaje, gdy skoroszyt ma jakiekolwiek obiekty pływające.
    /// </summary>
    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        if (workbook.Sheets.Any(s => s.Drawings.Count > 0 || s.Controls.Count > 0))
        {
            yield return NonTextRules.CriterionCheck("1.3.2");
        }
    }
}
