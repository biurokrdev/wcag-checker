using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace D2.Wcag.Tests;

// Elementy do testów standardu „Formatowanie z Galerii stylów” (style/1.0): fragmenty z formatowaniem bezpośrednim,
// akapity w podanym stylu, style z kontrolą w:qFormat / w:customStyle / w:semiHidden i motyw z kolorami.
internal sealed partial class DocxBuilder
{
    /// <summary>Fragment tekstu z formatowaniem bezpośrednim (dzieci w:rPr w podanej kolejności).</summary>
    public static Run Fragment(string text, params OpenXmlElement[] runProperties)
    {
        var run = new Run();
        if (runProperties.Length > 0)
        {
            run.Append(new RunProperties(runProperties));
        }

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    /// <summary>Akapit w podanym stylu akapitowym (w:pStyle) z podanymi fragmentami i innymi elementami treści.</summary>
    public DocxBuilder Styled(string styleId, params OpenXmlElement[] content)
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        paragraph.Append(content);
        return Add(paragraph);
    }

    /// <summary>Akapit z formatowaniem bezpośrednim akapitu (dzieci w:pPr) i podanymi fragmentami.</summary>
    public DocxBuilder Formatted(OpenXmlElement[] paragraphProperties, params OpenXmlElement[] content)
    {
        var paragraph = new Paragraph(new ParagraphProperties(paragraphProperties));
        paragraph.Append(content);
        return Add(paragraph);
    }

    /// <summary>Styl akapitowy oparty na „Normalny”.</summary>
    public static Style ParagraphStyle(
        string id, string name, bool gallery = true, bool custom = true, bool semiHidden = false, string? basedOn = "Normalny",
        StyleRunProperties? run = null, StyleParagraphProperties? paragraph = null) =>
        BuildStyle(StyleValues.Paragraph, id, name, gallery, custom, semiHidden, basedOn, null, run, paragraph);

    /// <summary>Styl znakowy; <paramref name="link"/> = połączony styl akapitowy (w:link).</summary>
    public static Style CharacterStyle(
        string id, string name, bool gallery = true, bool custom = true, bool semiHidden = false, string? link = null,
        StyleRunProperties? run = null) =>
        BuildStyle(StyleValues.Character, id, name, gallery, custom, semiHidden, null, link, run, null);

    /// <summary>
    /// Dodaje motyw z kolorami (Tekst 1 = 000000, Tło 1 = FFFFFF, Akcent 1 i 2 według parametrów). Wywołanie
    /// dokłada do treści pusty akapit, bo część motywu można dodać tylko przy budowaniu dokumentu.
    /// </summary>
    public DocxBuilder ThemeColors(string accent1, string accent2 = "ED7D31") => Add(main =>
    {
        var theme = main.AddNewPart<ThemePart>();
        theme.Theme = new A.Theme(new A.ThemeElements(
            new A.ColorScheme(
                new A.Dark1Color(new A.RgbColorModelHex { Val = "000000" }),
                new A.Light1Color(new A.RgbColorModelHex { Val = "FFFFFF" }),
                new A.Dark2Color(new A.RgbColorModelHex { Val = "44546A" }),
                new A.Light2Color(new A.RgbColorModelHex { Val = "E7E6E6" }),
                new A.Accent1Color(new A.RgbColorModelHex { Val = accent1 }),
                new A.Accent2Color(new A.RgbColorModelHex { Val = accent2 }),
                new A.Accent3Color(new A.RgbColorModelHex { Val = "A5A5A5" }),
                new A.Accent4Color(new A.RgbColorModelHex { Val = "FFC000" }),
                new A.Accent5Color(new A.RgbColorModelHex { Val = "5B9BD5" }),
                new A.Accent6Color(new A.RgbColorModelHex { Val = "70AD47" }),
                new A.Hyperlink(new A.RgbColorModelHex { Val = "0563C1" }),
                new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "954F72" }))
            { Name = "test" },
            new A.FontScheme(
                new A.MajorFont(new A.LatinFont { Typeface = "Calibri Light" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                new A.MinorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }))
            { Name = "test" },
            new A.FormatScheme { Name = "test" }))
        { Name = "test" };
        return new Paragraph();
    });

    private static Style BuildStyle(
        StyleValues type, string id, string name, bool gallery, bool custom, bool semiHidden, string? basedOn, string? link,
        StyleRunProperties? run, StyleParagraphProperties? paragraph)
    {
        var style = new Style(new StyleName { Val = name }) { Type = type, StyleId = id };
        if (custom)
        {
            style.CustomStyle = true;
        }

        if (basedOn is not null)
        {
            style.Append(new BasedOn { Val = basedOn });
        }

        if (link is not null)
        {
            style.Append(new LinkedStyle { Val = link });
        }

        if (semiHidden)
        {
            style.Append(new SemiHidden());
        }

        if (gallery)
        {
            style.Append(new PrimaryStyle());
        }

        if (paragraph is not null)
        {
            style.Append(paragraph);
        }

        if (run is not null)
        {
            style.Append(run);
        }

        return style;
    }
}
