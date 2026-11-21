using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// <c>XL-TEXT-IN-IMAGE</c> (1.4.5, wyłącznie kontrola ręczna – bez OCR): każdy obraz, który nie jest ukryty ani dekoracyjny, daje
/// pozycję checklisty „czy obraz nie zawiera tekstu”. Grupowanie: do <see cref="NonTextOptions.MaxImageChecksPerSheet"/> obrazów na
/// arkuszu – pozycja na obraz (z lokalizacją obrazu); powyżej – jedna pozycja na arkusz z liczbą obrazów i pierwszymi nazwami,
/// żeby checklista nie rozrosła się do setek wierszy.
/// </summary>
internal sealed class TextInImageRule : IXlsxRule
{
    public const string Code = "XL-TEXT-IN-IMAGE";

    private const string Hint =
        "Obejrzyj obraz. Jeśli zawiera tekst niosący informację (tabelę, zrzut ekranu, napis), wpisz ten tekst w komórkach albo w kształcie "
        + "zamiast obrazu, a co najmniej powtórz go w tekście alternatywnym (prawy przycisk myszy → Wyświetl tekst alternatywny). "
        + "Logotypy i tekst będący nieistotną częścią zdjęcia są wyjątkiem.";

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(Code, "Obraz może zawierać tekst", [XlsxWcagCriteria.Ref("1.4.5")], XlsxCheckType.Manual, Hint),
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            var images = NonTextRules.Objects(sheet)
                .Where(o => o.Object.Type == XlsxDrawingType.Image && !o.Hidden && !o.Object.Decorative && !o.GroupDecorative)
                .Select(o => o.Object)
                .ToList();
            if (images.Count == 0)
            {
                continue;
            }

            if (images.Count <= NonTextOptions.MaxImageChecksPerSheet)
            {
                foreach (var image in images)
                {
                    yield return new XlsxRuleHit(Code,
                        $"Czy obraz „{image.Name}” nie zawiera tekstu, który można było zapisać jako tekst w komórce lub kształcie (z wyjątkiem logotypów)?",
                        XlsxLocation.OfDrawing(sheet, image));
                }

                continue;
            }

            var names = string.Join(", ", images.Take(NonTextOptions.ImageNamesInGroupedCheck).Select(i => $"„{i.Name}”"));
            yield return new XlsxRuleHit(Code,
                $"Arkusz „{sheet.Name}” zawiera {images.Count} obrazów ({names} i kolejne). Czy żaden z nich nie zawiera tekstu, który można było zapisać jako tekst w komórce lub kształcie (z wyjątkiem logotypów)?",
                XlsxLocation.OfSheet(sheet))
            {
                Weight = images.Count,
            };
        }
    }
}
