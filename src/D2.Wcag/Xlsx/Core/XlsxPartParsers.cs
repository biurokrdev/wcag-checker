using System.Xml.Linq;

namespace D2.Wcag.Xlsx;

/// <summary>Parsery małych części pakietu wczytywanych jako drzewo XML: style, motyw, tabele, komentarze, właściwości.</summary>
internal static class XlsxPartParsers
{
    public static XlsxColor? ParseColor(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var rgb = XlsxXml.NullIfEmpty(element.Attr("rgb"));
        var theme = XlsxXml.Int(element.Attr("theme"));
        var indexed = XlsxXml.Int(element.Attr("indexed"));
        var auto = XlsxXml.Bool(element.Attr("auto"));
        var tint = XlsxXml.Dbl(element.Attr("tint")) ?? 0;
        return rgb is null && theme is null && indexed is null && !auto ? null : new XlsxColor(rgb, theme, tint, indexed, auto);
    }

    /// <summary>
    /// Motyw: w pliku kolory stoją w kolejności dk1, lt1, dk2, lt2, accent1–6, hlink, folHlink; wynik ma kolejność indeksów
    /// arkusza (lt1, dk1, lt2, dk2, …). Brakujące kolory uzupełnia motyw „Office”.
    /// </summary>
    public static XlsxTheme ParseTheme(XElement? root)
    {
        var scheme = root.El("themeElements").El("clrScheme");
        if (scheme is null)
        {
            return XlsxTheme.Office;
        }

        string[] names = ["lt1", "dk1", "lt2", "dk2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink"];
        var colors = new string[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            var entry = scheme.El(names[i]);
            var hex = XlsxColors.NormalizeHex(entry.El("srgbClr").Attr("val"))
                ?? XlsxColors.NormalizeHex(entry.El("sysClr").Attr("lastClr"))
                ?? entry.El("sysClr").Attr("val") switch
                {
                    "windowText" => XlsxColors.Black,
                    "window" => XlsxColors.White,
                    _ => null,
                };
            colors[i] = hex ?? XlsxTheme.Office.ColorsHex[i];
        }

        return new XlsxTheme(colors);
    }

    /// <summary>
    /// Style. Reguła dziedziczenia: format komórki (<c>cellXfs/xf</c>) używa własnych <c>fontId</c>, <c>fillId</c>,
    /// <c>numFmtId</c> oraz własnych <c>alignment</c>/<c>protection</c>, gdy są zapisane – niezależnie od atrybutów
    /// <c>apply*</c>, bo programy zapisują w <c>cellXfs</c> identyfikatory już rozwinięte (tak też czyta je Excel). Wartość
    /// ze stylu nazwanego (<c>cellStyleXfs[xfId]</c>) jest brana tylko wtedy, gdy formatowi komórki danego atrybutu albo
    /// elementu brakuje.
    /// </summary>
    public static XlsxStyles ParseStyles(XElement? root)
    {
        if (root is null)
        {
            return XlsxStyles.Default;
        }

        var fonts = root.El("fonts").Els("font").Select(f => ParseFont(f, differential: false)!).ToList();
        var fills = root.El("fills").Els("fill").Select(f => ParseFill(f, differential: false)!).ToList();
        var numberFormats = new Dictionary<int, string>();
        foreach (var format in root.El("numFmts").Els("numFmt"))
        {
            if (XlsxXml.Int(format.Attr("numFmtId")) is { } id)
            {
                numberFormats[id] = format.Attr("formatCode") ?? "";
            }
        }

        var styleXfs = root.El("cellStyleXfs").Els("xf").ToList();
        var cellFormats = new List<XlsxCellFormat>();
        foreach (var xf in root.El("cellXfs").Els("xf"))
        {
            var parent = XlsxXml.Int(xf.Attr("xfId")) is { } xfId && xfId >= 0 && xfId < styleXfs.Count ? styleXfs[xfId] : null;
            var fontId = XlsxXml.Int(xf.Attr("fontId")) ?? XlsxXml.Int(parent.Attr("fontId")) ?? 0;
            var fillId = XlsxXml.Int(xf.Attr("fillId")) ?? XlsxXml.Int(parent.Attr("fillId")) ?? 0;
            var numFmtId = XlsxXml.Int(xf.Attr("numFmtId")) ?? XlsxXml.Int(parent.Attr("numFmtId")) ?? 0;
            var alignment = xf.El("alignment") ?? parent.El("alignment");
            var protection = xf.El("protection") ?? parent.El("protection");
            cellFormats.Add(new XlsxCellFormat(
                cellFormats.Count,
                fontId >= 0 && fontId < fonts.Count ? fonts[fontId] : XlsxStyles.DefaultFont,
                fillId >= 0 && fillId < fills.Count ? fills[fillId] : XlsxStyles.NoFill,
                numFmtId,
                numberFormats.TryGetValue(numFmtId, out var code) ? code : XlsxStyles.BuiltInNumberFormat(numFmtId),
                XlsxXml.Bool(protection.Attr("hidden")),
                XlsxXml.Bool(protection.Attr("locked"), true),
                XlsxXml.NullIfEmpty(alignment.Attr("horizontal")),
                XlsxXml.Bool(alignment.Attr("wrapText"))));
        }

        var dxfs = root.El("dxfs").Els("dxf")
            .Select(dxf => new XlsxDxf(
                ParseFont(dxf.El("font"), differential: true),
                ParseFill(dxf.El("fill"), differential: true),
                dxf.El("border") is { } border && border.Elements().Any(side => side.Attr("style") is not (null or "none")),
                XlsxXml.NullIfEmpty(dxf.El("numFmt").Attr("formatCode"))))
            .ToList();

        List<string>? indexed = null;
        if (root.El("colors").El("indexedColors") is { } palette)
        {
            indexed = palette.Els("rgbColor").Select(c => XlsxColors.NormalizeHex(c.Attr("rgb")) ?? XlsxColors.Black).ToList();
        }

        return new XlsxStyles(cellFormats, dxfs, indexed is { Count: > 0 } ? indexed : null);
    }

    /// <summary>Czcionka; w stylu różnicowym (<c>dxf</c>) brak rozmiaru zapisujemy jako <c>SizePt = 0</c> („jak w komórce”).</summary>
    private static XlsxFont? ParseFont(XElement? font, bool differential)
    {
        if (font is null)
        {
            return null;
        }

        var underline = font.El("u") is { } u && u.Attr("val") != "none";
        return new XlsxFont(
            XlsxXml.NullIfEmpty((font.El("name") ?? font.El("rFont")).Attr("val")),
            XlsxXml.Dbl(font.El("sz").Attr("val")) ?? (differential ? 0 : 11),
            font.El("b").Flag(),
            font.El("i").Flag(),
            underline,
            font.El("strike").Flag(),
            ParseColor(font.El("color")));
    }

    /// <summary>
    /// Wypełnienie. W stylu różnicowym pełny kolor zapisany jest w <c>bgColor</c>, a <c>patternType</c> bywa pominięty –
    /// normalizujemy to do postaci jak w zwykłym wypełnieniu (<c>solid</c>, kolor w <see cref="XlsxFill.Foreground"/>).
    /// </summary>
    private static XlsxFill? ParseFill(XElement? fill, bool differential)
    {
        if (fill is null)
        {
            return differential ? null : XlsxStyles.NoFill;
        }

        if (fill.El("gradientFill") is { } gradient)
        {
            var stops = gradient.Els("stop").Select(s => ParseColor(s.El("color"))).Where(c => c is not null).ToList();
            return new XlsxFill("gradient", stops.FirstOrDefault(), stops.LastOrDefault());
        }

        var pattern = fill.El("patternFill");
        if (pattern is null)
        {
            return differential ? null : XlsxStyles.NoFill;
        }

        var type = XlsxXml.NullIfEmpty(pattern.Attr("patternType"));
        var foreground = ParseColor(pattern.El("fgColor"));
        var background = ParseColor(pattern.El("bgColor"));
        if (differential)
        {
            type ??= foreground is null && background is null ? "none" : "solid";
            if (type == "solid" && background is not null)
            {
                return new XlsxFill("solid", background, null);
            }
        }

        return new XlsxFill(type ?? "none", foreground, background);
    }

    public static XlsxTable? ParseTable(XElement? root)
    {
        if (root is null || !RangeRef.TryParse(root.Attr("ref"), out var range))
        {
            return null;
        }

        var name = root.Attr("name") ?? root.Attr("displayName") ?? "";
        var extension = root.Desc("table").FirstOrDefault(e => e.Attr("altText") is not null || e.Attr("altTextSummary") is not null);
        return new XlsxTable(
            name,
            root.Attr("displayName") ?? name,
            range,
            XlsxXml.Int(root.Attr("headerRowCount")) ?? 1,
            XlsxXml.Int(root.Attr("totalsRowCount")) ?? 0,
            root.El("tableColumns").Els("tableColumn").Select(c => c.Attr("name") ?? "").ToList(),
            XlsxXml.NullIfEmpty(extension.Attr("altText")),
            XlsxXml.NullIfEmpty(extension.Attr("altTextSummary")),
            XlsxXml.NullIfEmpty(root.El("tableStyleInfo").Attr("name")));
    }

    /// <summary>Notatki (<c>commentsN.xml</c>). Zaślepki komentarzy wątkowych (autor „tc=…”) są pomijane – treść daje część wątkowa.</summary>
    public static IEnumerable<XlsxComment> ParseComments(XElement? root)
    {
        if (root is null)
        {
            yield break;
        }

        var authors = root.El("authors").Els("author").Select(a => a.Value).ToList();
        foreach (var comment in root.El("commentList").Els("comment"))
        {
            if (!CellRef.TryParse(comment.Attr("ref"), out var reference))
            {
                continue;
            }

            var author = XlsxXml.Int(comment.Attr("authorId")) is { } id && id >= 0 && id < authors.Count ? authors[id] : "";
            if (author.StartsWith("tc=", StringComparison.Ordinal))
            {
                continue;
            }

            yield return new XlsxComment(reference, author, StringItemText(comment.El("text")), false);
        }
    }

    public static IEnumerable<XlsxComment> ParseThreadedComments(XElement? root, IReadOnlyDictionary<string, string> persons)
    {
        foreach (var comment in root.Els("threadedComment"))
        {
            if (!CellRef.TryParse(comment.Attr("ref"), out var reference))
            {
                continue;
            }

            var author = comment.Attr("personId") is { } person && persons.TryGetValue(person, out var name) ? name : "";
            yield return new XlsxComment(reference, author, comment.El("text")?.Value ?? "", true);
        }
    }

    public static Dictionary<string, string> ParsePersons(XElement? root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in root.Els("person"))
        {
            if (person.Attr("id") is { } id)
            {
                result[id] = person.Attr("displayName") ?? "";
            }
        }

        return result;
    }

    public static XlsxDocumentProperties ParseProperties(XElement? core, XElement? app) => new(
        Text(core.El("title")),
        Text(core.El("subject")),
        Text(core.El("creator")),
        Text(core.El("language")),
        Text(core.El("description")),
        Text(core.El("keywords")),
        Text(app.El("Application")));

    /// <summary>Tekst elementu typu „string item” (<c>t</c> oraz <c>r/t</c>), bez zapisu fonetycznego.</summary>
    private static string StringItemText(XElement? item)
    {
        if (item is null)
        {
            return "";
        }

        return string.Concat(item.Elements()
            .Where(e => e.Name.LocalName is "t" or "r")
            .Select(e => e.Name.LocalName == "t" ? e.Value : string.Concat(e.Els("t").Select(t => t.Value))));
    }

    private static string? Text(XElement? element) => string.IsNullOrWhiteSpace(element?.Value) ? null : element.Value.Trim();
}
