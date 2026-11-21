using System.Xml;
using System.Xml.Linq;

namespace D2.Wcag.Xlsx;

/// <summary>Rysunki (<c>drawingN.xml</c>), wykresy (<c>chartN.xml</c>) i stary zapis VML kontrolek.</summary>
internal sealed class XlsxDrawingParser
{
    private readonly XlsxPackage _package;
    private readonly XlsxTheme _theme;

    public XlsxDrawingParser(XlsxPackage package, XlsxTheme theme)
    {
        _package = package;
        _theme = theme;
    }

    public (List<XlsxDrawingObject> Objects, List<XlsxChart> Charts) ParseDrawing(string drawingPart)
    {
        var objects = new List<XlsxDrawingObject>();
        var charts = new List<XlsxChart>();
        var root = _package.LoadXml(drawingPart);
        if (root is null)
        {
            return (objects, charts);
        }

        var relationships = _package.Relationships(drawingPart).ToDictionary(r => r.Id, StringComparer.Ordinal);
        foreach (var anchor in root.Flat())
        {
            if (anchor.Name.LocalName is not ("twoCellAnchor" or "oneCellAnchor" or "absoluteAnchor"))
            {
                continue;
            }

            var from = Marker(anchor.El("from"));
            var to = Marker(anchor.El("to"));
            foreach (var child in anchor.Flat())
            {
                AddObject(child, from, to, null, relationships, objects, charts);
            }
        }

        return (objects, charts);
    }

    /// <summary>Kształty VML (<c>vmlDrawingN.vml</c>): identyfikator → etykieta, tekst alternatywny, rodzaj, kotwica, komórka połączona.</summary>
    public List<VmlShape> ParseVml(string vmlPart)
    {
        var result = new List<VmlShape>();
        XElement? root;
        try
        {
            root = _package.LoadXml(vmlPart);
        }
        catch (XmlException exception) when (!XlsxPackage.IsProhibitedDtd(exception))
        {
            // VML pisany przez stare programy nie zawsze jest poprawnym XML; brak etykiet nie blokuje audytu (DTD nadal jest odmową).
            return result;
        }

        foreach (var shape in root.Desc("shape"))
        {
            var client = shape.El("ClientData");
            var type = client.Attr("ObjectType");
            if (type is null)
            {
                continue;
            }

            CellRef? anchor = null;
            var parts = client.El("Anchor")?.Value.Split(',', StringSplitOptions.TrimEntries);
            if (parts is { Length: >= 3 } && XlsxXml.Int(parts[0]) is { } column && XlsxXml.Int(parts[2]) is { } row
                && column is >= 0 and < CellRef.MaxColumn && row is >= 0 and < CellRef.MaxRow)
            {
                anchor = new CellRef(row + 1, column + 1);
            }

            var label = shape.El("textbox") is { } textbox ? Collapse(textbox.Value) : null;
            result.Add(new VmlShape(shape.Attr("id") ?? "", type, XlsxXml.NullIfEmpty(label), XlsxXml.NullIfEmpty(shape.Attr("alt")),
                anchor, XlsxXml.NullIfEmpty(client.El("FmlaLink")?.Value.Trim())));
        }

        return result;
    }

    private static CellRef? Marker(XElement? marker)
    {
        if (XlsxXml.Int(marker.El("col")?.Value) is { } column && XlsxXml.Int(marker.El("row")?.Value) is { } row
            && column is >= 0 and < CellRef.MaxColumn && row is >= 0 and < CellRef.MaxRow)
        {
            return new CellRef(row + 1, column + 1);
        }

        return null;
    }

    private void AddObject(XElement element, CellRef? from, CellRef? to, string? parentGroupId,
        Dictionary<string, XlsxRelationship> relationships, List<XlsxDrawingObject> objects, List<XlsxChart> charts)
    {
        var kind = element.Name.LocalName;
        if (kind is not ("pic" or "sp" or "graphicFrame" or "grpSp" or "cxnSp"))
        {
            return;
        }

        var nonVisual = element.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal));
        var properties = nonVisual.El("cNvPr");
        var id = properties.Attr("id") ?? "";
        var name = properties.Attr("name") ?? "";
        var description = properties.Attr("descr");
        var title = XlsxXml.NullIfEmpty(properties.Attr("title"));
        var hidden = XlsxXml.Bool(properties.Attr("hidden"));
        var decorative = properties.Desc("decorative").Any(d => XlsxXml.Bool(d.Attr("val")));
        var isControl = properties.Desc("compatExt").Any();
        string? hyperlink = null;
        if (properties.El("hlinkClick").RelAttr("id") is { } linkId && relationships.TryGetValue(linkId, out var link))
        {
            hyperlink = link.Target;
        }

        var type = XlsxDrawingType.Shape;
        var text = "";
        string? imageFile = null;
        string? chartPart = null;
        switch (kind)
        {
            case "pic":
                type = XlsxDrawingType.Image;
                var blip = element.Desc("blip").FirstOrDefault();
                if ((blip.RelAttr("embed") ?? blip.RelAttr("link")) is { } imageId && relationships.TryGetValue(imageId, out var image))
                {
                    imageFile = image.Target[(image.Target.LastIndexOf('/') + 1)..];
                }

                break;
            case "sp":
                text = ShapeText(element.El("txBody"));
                type = XlsxXml.Bool(nonVisual.El("cNvSpPr").Attr("txBox")) ? XlsxDrawingType.TextBox : XlsxDrawingType.Shape;
                break;
            case "graphicFrame":
                var chart = element.Desc("graphicData").SelectMany(d => d.Elements()).FirstOrDefault(e => e.Name.LocalName == "chart");
                if (chart.RelAttr("id") is { } chartId && relationships.TryGetValue(chartId, out var chartRelationship) && !chartRelationship.External)
                {
                    type = XlsxDrawingType.Chart;
                    chartPart = chartRelationship.Target;
                }

                break;
        }

        if (isControl)
        {
            type = XlsxDrawingType.Control;
        }

        var result = new XlsxDrawingObject(id, name, type, description, title, decorative, hidden, from, to, text, imageFile, chartPart,
            hyperlink, kind == "grpSp")
        {
            ParentGroupId = parentGroupId,
        };
        objects.Add(result);

        if (chartPart is not null)
        {
            charts.Add(ParseChart(chartPart, result));
        }

        if (kind == "grpSp")
        {
            foreach (var child in element.Flat())
            {
                AddObject(child, from, to, id, relationships, objects, charts);
            }
        }
    }

    private static string ShapeText(XElement? body)
    {
        if (body is null)
        {
            return "";
        }

        var paragraphs = body.Els("p")
            .Select(p => string.Concat(p.Descendants().Where(e => e.Name.LocalName is "t" or "br").Select(e => e.Name.LocalName == "br" ? "\n" : e.Value)))
            .Where(p => p.Length > 0);
        return string.Join("\n", paragraphs).Trim();
    }

    private static string Collapse(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private XlsxChart ParseChart(string chartPart, XlsxDrawingObject frame)
    {
        XElement? root;
        try
        {
            root = _package.LoadXml(chartPart);
        }
        catch (XmlException exception) when (!XlsxPackage.IsProhibitedDtd(exception))
        {
            root = null;
        }

        var chart = root?.Name.LocalName == "chart" ? root : root.El("chart");
        var plotArea = chart.El("plotArea");
        var titleElement = chart.El("title");
        var groups = plotArea.Flat().Where(e => e.Name.LocalName.EndsWith("Chart", StringComparison.Ordinal)).ToList();
        var types = groups.Select(g => g.Name.LocalName[..^"Chart".Length]).Distinct().ToList();

        var series = new List<XlsxChartSeries>();
        var groupLabels = false;
        foreach (var group in groups)
        {
            groupLabels |= ShowsLabels(group.El("dLbls"));
            foreach (var ser in group.Els("ser"))
            {
                var shape = ser.El("spPr");
                var line = shape.El("ln");
                var pattern = shape.El("pattFill");
                series.Add(new XlsxChartSeries(
                    XlsxXml.Int(ser.El("idx").Attr("val")) ?? series.Count,
                    XlsxXml.NullIfEmpty(TextOf(ser.El("tx"))),
                    Color(shape.El("solidFill")) ?? Color(pattern.El("fgClr")),
                    Color(line.El("solidFill")),
                    pattern is not null,
                    XlsxXml.NullIfEmpty(ser.El("marker").El("symbol").Attr("val")),
                    XlsxXml.NullIfEmpty(line.El("prstDash").Attr("val")),
                    ShowsLabels(ser.El("dLbls"))));
            }
        }

        var axes = plotArea.Flat()
            .Where(e => e.Name.LocalName is "catAx" or "valAx" or "dateAx" or "serAx")
            .Select(axis => new XlsxChartAxis(
                axis.Name.LocalName switch { "catAx" => "category", "valAx" => "value", "dateAx" => "date", _ => "series" },
                axis.El("title") is { } axisTitle ? TextOf(axisTitle.El("tx")) ?? "" : null,
                axis.El("delete").Flag()))
            .ToList();

        return new XlsxChart(
            frame.Name,
            titleElement is null ? null : TextOf(titleElement.El("tx")) ?? "",
            chart.El("autoTitleDeleted").Flag(),
            axes,
            series,
            types.Count == 0 ? "unknown" : string.Join('+', types),
            chart.El("legend") is not null,
            groupLabels || series.Any(s => s.HasDataLabels),
            frame.Description,
            Color(plotArea.El("spPr").El("solidFill")),
            frame);
    }

    /// <summary>Tekst tytułu albo nazwy serii: tekst wpisany (<c>rich</c>), wartość z pamięci podręcznej odwołania (<c>strRef/strCache</c>) albo <c>v</c>.</summary>
    private static string? TextOf(XElement? tx)
    {
        if (tx is null)
        {
            return null;
        }

        if (tx.El("rich") is { } rich)
        {
            return string.Join(" ", rich.Els("p").Select(p => string.Concat(p.Desc("t").Select(t => t.Value))).Where(p => p.Length > 0)).Trim();
        }

        if (tx.El("strRef").El("strCache") is { } cache)
        {
            return string.Join(" ", cache.Els("pt").Select(pt => pt.El("v")?.Value ?? "")).Trim();
        }

        return tx.El("v")?.Value.Trim();
    }

    private static bool ShowsLabels(XElement? labels)
    {
        if (labels is null || labels.El("delete").Flag())
        {
            return false;
        }

        return labels.Elements().Any(e => e.Name.LocalName is "showVal" or "showCatName" or "showSerName" or "showPercent" && e.Flag());
    }

    /// <summary>Kolor DrawingML: <c>srgbClr</c>, <c>schemeClr</c> (z motywu, z <c>lumMod</c>/<c>lumOff</c>) albo <c>sysClr</c>.</summary>
    private string? Color(XElement? container)
    {
        if (container is null)
        {
            return null;
        }

        if (container.El("srgbClr") is { } rgb)
        {
            return Adjust(XlsxColors.NormalizeHex(rgb.Attr("val")), rgb);
        }

        if (container.El("sysClr") is { } system)
        {
            return XlsxColors.NormalizeHex(system.Attr("lastClr"));
        }

        if (container.El("schemeClr") is { } scheme)
        {
            var index = scheme.Attr("val") switch
            {
                "bg1" or "lt1" => 0,
                "tx1" or "dk1" => 1,
                "bg2" or "lt2" => 2,
                "tx2" or "dk2" => 3,
                "accent1" => 4,
                "accent2" => 5,
                "accent3" => 6,
                "accent4" => 7,
                "accent5" => 8,
                "accent6" => 9,
                "hlink" => 10,
                "folHlink" => 11,
                _ => -1,
            };
            return index < 0 ? null : Adjust(_theme.ColorsHex[index], scheme);
        }

        return null;
    }

    private static string? Adjust(string? hex, XElement color)
    {
        if (hex is null)
        {
            return null;
        }

        var modulation = XlsxXml.Dbl(color.El("lumMod").Attr("val"));
        var offset = XlsxXml.Dbl(color.El("lumOff").Attr("val"));
        return modulation is null && offset is null
            ? hex
            : XlsxColors.ApplyLuminance(hex, (modulation ?? 100000) / 100000, (offset ?? 0) / 100000);
    }
}

internal sealed record VmlShape(string Id, string ObjectType, string? Label, string? AltText, CellRef? Anchor, string? LinkedCell);
