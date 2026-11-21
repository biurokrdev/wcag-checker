using System.Globalization;
using System.Text;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Fixtures.Xlsx;

/// <summary>Obiekt rysunku fixtury: obraz, kształt, pole tekstowe, ramka wykresu albo grupa.</summary>
public sealed class FxDrawingItem
{
    /// <summary>pic | sp | graphicFrame | grpSp</summary>
    public string Kind { get; init; } = "pic";

    public string Name { get; init; } = "";

    public int Id { get; init; }

    /// <summary>Tekst alternatywny (<c>descr</c>); <c>null</c> = atrybutu nie ma.</summary>
    public string? Description { get; init; }

    public string? Title { get; init; }

    public bool Decorative { get; init; }

    public bool Hidden { get; init; }

    public string Text { get; init; } = "";

    public bool TextBox { get; init; }

    public string File { get; init; } = "image1.png";

    public string? HyperlinkUrl { get; init; }

    public FxChart? ChartModel { get; init; }

    public List<FxDrawingItem> Children { get; } = [];

    internal string From { get; private set; } = "A1";

    internal string To { get; private set; } = "B2";

    public static FxDrawingItem Picture(string name, int id, string? description = null, bool decorative = false, string? title = null,
        string file = "image1.png", string? hyperlink = null, bool hidden = false) =>
        new() { Kind = "pic", Name = name, Id = id, Description = description, Decorative = decorative, Title = title, File = file, HyperlinkUrl = hyperlink, Hidden = hidden };

    public static FxDrawingItem Shape(string name, int id, string text = "", bool textBox = false, string? description = null,
        bool decorative = false, string? hyperlink = null) =>
        new() { Kind = "sp", Name = name, Id = id, Text = text, TextBox = textBox, Description = description, Decorative = decorative, HyperlinkUrl = hyperlink };

    internal FxDrawingItem At(string from, string to)
    {
        From = from;
        To = to;
        return this;
    }
}

/// <summary>Seria wykresu z wartościami w pamięci podręcznej.</summary>
public sealed record FxSeries(string Name, string NameRef, string CategoriesRef, string ValuesRef, string[] Categories, double[] Values)
{
    /// <summary>Kolor wypełnienia „RRGGBB”; <c>null</c> = automatyczny.</summary>
    public string? Fill { get; init; }

    /// <summary>Wzór wypełnienia (np. „wdUpDiag”) – rozróżnienie serii nie tylko kolorem.</summary>
    public string? Pattern { get; init; }

    /// <summary>Kolor linii „RRGGBB”.</summary>
    public string? Line { get; init; }

    /// <summary>Styl kreski linii, np. „dash”, „sysDot”.</summary>
    public string? Dash { get; init; }

    /// <summary>Symbol znacznika (wykres liniowy): „circle”, „square”, „triangle”, „none”…</summary>
    public string? Marker { get; init; }

    public bool DataLabels { get; init; }
}

/// <summary>Wykres kolumnowy albo liniowy z osiami kategorii i wartości.</summary>
public sealed class FxChart
{
    /// <summary>bar | line</summary>
    public string Type { get; init; } = "bar";

    /// <summary>Tytuł; <c>null</c> = brak elementu tytułu.</summary>
    public string? Title { get; init; }

    public bool AutoTitleDeleted { get; init; }

    public string? CategoryAxisTitle { get; init; }

    public string? ValueAxisTitle { get; init; }

    public bool Legend { get; init; } = true;

    /// <summary>Kolor tła obszaru kreślenia „RRGGBB”; <c>null</c> = automatyczny.</summary>
    public string? PlotAreaFill { get; init; }

    public List<FxSeries> Series { get; } = [];

    internal string ToXml(FixtureXlsx owner)
    {
        var xml = new StringBuilder();
        xml.Append(CultureInfo.InvariantCulture, $"{FixtureXlsx.XmlHeader}<c:chartSpace xmlns:c=\"{owner.ChartNs}\" xmlns:a=\"{owner.DrawingNs}\" xmlns:r=\"{owner.RelNs}\">");
        xml.Append("<c:roundedCorners val=\"0\"/><c:chart>");
        if (Title is not null)
        {
            xml.Append(TitleXml(Title));
        }

        xml.Append(CultureInfo.InvariantCulture, $"<c:autoTitleDeleted val=\"{(AutoTitleDeleted ? 1 : 0)}\"/><c:plotArea><c:layout/>");
        xml.Append(Type == "line"
            ? "<c:lineChart><c:grouping val=\"standard\"/><c:varyColors val=\"0\"/>"
            : "<c:barChart><c:barDir val=\"col\"/><c:grouping val=\"clustered\"/><c:varyColors val=\"0\"/>");
        for (var i = 0; i < Series.Count; i++)
        {
            var series = Series[i];
            xml.Append(CultureInfo.InvariantCulture, $"<c:ser><c:idx val=\"{i}\"/><c:order val=\"{i}\"/>");
            xml.Append(CultureInfo.InvariantCulture, $"<c:tx><c:strRef><c:f>{FixtureXlsx.Esc(series.NameRef)}</c:f><c:strCache><c:ptCount val=\"1\"/><c:pt idx=\"0\"><c:v>{FixtureXlsx.Esc(series.Name)}</c:v></c:pt></c:strCache></c:strRef></c:tx>");
            if (series.Fill is not null || series.Line is not null || series.Dash is not null || series.Pattern is not null)
            {
                xml.Append("<c:spPr>");
                if (series.Pattern is not null)
                {
                    xml.Append(CultureInfo.InvariantCulture, $"<a:pattFill prst=\"{series.Pattern}\"><a:fgClr><a:srgbClr val=\"{series.Fill ?? "000000"}\"/></a:fgClr><a:bgClr><a:srgbClr val=\"FFFFFF\"/></a:bgClr></a:pattFill>");
                }
                else if (series.Fill is not null)
                {
                    xml.Append(CultureInfo.InvariantCulture, $"<a:solidFill><a:srgbClr val=\"{series.Fill}\"/></a:solidFill>");
                }

                if (series.Line is not null || series.Dash is not null)
                {
                    xml.Append("<a:ln w=\"28575\">");
                    if (series.Line is not null)
                    {
                        xml.Append(CultureInfo.InvariantCulture, $"<a:solidFill><a:srgbClr val=\"{series.Line}\"/></a:solidFill>");
                    }

                    if (series.Dash is not null)
                    {
                        xml.Append(CultureInfo.InvariantCulture, $"<a:prstDash val=\"{series.Dash}\"/>");
                    }

                    xml.Append("</a:ln>");
                }

                xml.Append("</c:spPr>");
            }

            if (Type == "line")
            {
                xml.Append(CultureInfo.InvariantCulture, $"<c:marker><c:symbol val=\"{series.Marker ?? "none"}\"/></c:marker>");
            }
            else
            {
                xml.Append("<c:invertIfNegative val=\"0\"/>");
            }

            if (series.DataLabels)
            {
                xml.Append("<c:dLbls><c:showLegendKey val=\"0\"/><c:showVal val=\"1\"/><c:showCatName val=\"0\"/><c:showSerName val=\"0\"/><c:showPercent val=\"0\"/><c:showBubbleSize val=\"0\"/></c:dLbls>");
            }

            xml.Append(CultureInfo.InvariantCulture, $"<c:cat><c:strRef><c:f>{FixtureXlsx.Esc(series.CategoriesRef)}</c:f><c:strCache><c:ptCount val=\"{series.Categories.Length}\"/>");
            for (var p = 0; p < series.Categories.Length; p++)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<c:pt idx=\"{p}\"><c:v>{FixtureXlsx.Esc(series.Categories[p])}</c:v></c:pt>");
            }

            xml.Append(CultureInfo.InvariantCulture, $"</c:strCache></c:strRef></c:cat><c:val><c:numRef><c:f>{FixtureXlsx.Esc(series.ValuesRef)}</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"{series.Values.Length}\"/>");
            for (var p = 0; p < series.Values.Length; p++)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<c:pt idx=\"{p}\"><c:v>{series.Values[p].ToString("R", CultureInfo.InvariantCulture)}</c:v></c:pt>");
            }

            xml.Append("</c:numCache></c:numRef></c:val>");
            if (Type == "line")
            {
                xml.Append("<c:smooth val=\"0\"/>");
            }

            xml.Append("</c:ser>");
        }

        xml.Append(Type == "line"
            ? "<c:marker val=\"1\"/><c:axId val=\"111111111\"/><c:axId val=\"222222222\"/></c:lineChart>"
            : "<c:gapWidth val=\"150\"/><c:axId val=\"111111111\"/><c:axId val=\"222222222\"/></c:barChart>");
        xml.Append("<c:catAx><c:axId val=\"111111111\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"b\"/>");
        if (CategoryAxisTitle is not null)
        {
            xml.Append(TitleXml(CategoryAxisTitle));
        }

        xml.Append("<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"out\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:crossAx val=\"222222222\"/><c:crosses val=\"autoZero\"/><c:auto val=\"1\"/><c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/></c:catAx>");
        xml.Append("<c:valAx><c:axId val=\"222222222\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"l\"/><c:majorGridlines/>");
        if (ValueAxisTitle is not null)
        {
            xml.Append(TitleXml(ValueAxisTitle));
        }

        xml.Append("<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"out\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:crossAx val=\"111111111\"/><c:crosses val=\"autoZero\"/><c:crossBetween val=\"between\"/></c:valAx>");
        if (PlotAreaFill is not null)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<c:spPr><a:solidFill><a:srgbClr val=\"{PlotAreaFill}\"/></a:solidFill></c:spPr>");
        }

        xml.Append("</c:plotArea>");
        if (Legend)
        {
            xml.Append("<c:legend><c:legendPos val=\"b\"/><c:overlay val=\"0\"/></c:legend>");
        }

        xml.Append("<c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart></c:chartSpace>");
        return xml.ToString();
    }

    private static string TitleXml(string text) =>
        $"<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"pl-PL\"/><a:t>{FixtureXlsx.Esc(text)}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title>";
}

internal static class FxDrawingWriter
{
    private const string DecorativeExt =
        "<a:extLst><a:ext uri=\"{C183D7F6-B498-43B3-948B-1728B52AA6E4}\"><adec:decorative xmlns:adec=\"http://schemas.microsoft.com/office/drawing/2017/decorative\" val=\"1\"/></a:ext></a:extLst>";

    public static string Drawing(FixtureXlsx owner, List<FxDrawingItem> items, List<FxControl> controls, bool chartSheet,
        SortedDictionary<string, byte[]> parts, SortedDictionary<string, string> overrides, SortedDictionary<string, string> defaults,
        FixtureXlsx.PartCounters counters, out FixtureXlsx.Rels rels)
    {
        rels = new FixtureXlsx.Rels(owner);
        var xml = new StringBuilder();
        xml.Append(CultureInfo.InvariantCulture, $"{FixtureXlsx.XmlHeader}<xdr:wsDr xmlns:xdr=\"{owner.SheetDrawingNs}\" xmlns:a=\"{owner.DrawingNs}\" xmlns:r=\"{owner.RelNs}\">");
        foreach (var item in items)
        {
            if (chartSheet)
            {
                xml.Append("<xdr:absoluteAnchor><xdr:pos x=\"0\" y=\"0\"/><xdr:ext cx=\"9300000\" cy=\"6000000\"/>");
                Object(xml, owner, item, rels, parts, overrides, defaults, counters);
                xml.Append("<xdr:clientData/></xdr:absoluteAnchor>");
                continue;
            }

            xml.Append("<xdr:twoCellAnchor editAs=\"oneCell\">").Append(Marker("from", item.From)).Append(Marker("to", item.To));
            Object(xml, owner, item, rels, parts, overrides, defaults, counters);
            xml.Append("<xdr:clientData/></xdr:twoCellAnchor>");
        }

        // Kontrolki mają w rysunku ukryty kształt z rozszerzeniem a14:compatExt (tak zapisuje je Excel 2010+).
        foreach (var control in controls)
        {
            xml.Append("<mc:AlternateContent xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"><mc:Choice xmlns:a14=\"http://schemas.microsoft.com/office/drawing/2010/main\" Requires=\"a14\">");
            xml.Append("<xdr:twoCellAnchor editAs=\"oneCell\">").Append(Marker("from", control.From)).Append(Marker("to", control.To));
            xml.Append(CultureInfo.InvariantCulture, $"<xdr:sp macro=\"\" textlink=\"\"><xdr:nvSpPr><xdr:cNvPr id=\"{control.ShapeId}\" name=\"{FixtureXlsx.Esc(control.Name)}\" hidden=\"1\">");
            xml.Append(CultureInfo.InvariantCulture, $"<a:extLst><a:ext uri=\"{{63B3BB69-23CF-44E3-9099-C40C66FF867C}}\"><a14:compatExt spid=\"_x0000_s{control.ShapeId}\"/></a:ext></a:extLst></xdr:cNvPr><xdr:cNvSpPr/></xdr:nvSpPr>");
            xml.Append("<xdr:spPr bwMode=\"auto\"><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/></xdr:spPr>");
            if (!string.IsNullOrEmpty(control.Label))
            {
                xml.Append(CultureInfo.InvariantCulture, $"<xdr:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:rPr lang=\"pl-PL\"/><a:t>{FixtureXlsx.Esc(control.Label)}</a:t></a:r></a:p></xdr:txBody>");
            }

            xml.Append("</xdr:sp><xdr:clientData/></xdr:twoCellAnchor></mc:Choice><mc:Fallback/></mc:AlternateContent>");
        }

        xml.Append("</xdr:wsDr>");
        return xml.ToString();
    }

    /// <summary>Rysunek VML: kształty notatek (t202) i kontrolek (t201).</summary>
    public static string Vml(List<string> commentRefs, List<FxControl> controls)
    {
        var xml = new StringBuilder();
        xml.Append("<xml xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\">");
        xml.Append("<o:shapelayout v:ext=\"edit\"><o:idmap v:ext=\"edit\" data=\"1\"/></o:shapelayout>");
        if (controls.Count > 0)
        {
            xml.Append("<v:shapetype id=\"_x0000_t201\" coordsize=\"21600,21600\" o:spt=\"201\" path=\"m,l,21600r21600,l21600,xe\"><v:stroke joinstyle=\"miter\"/><v:path shadowok=\"f\" o:extrusionok=\"f\" strokeok=\"f\" fillok=\"f\" o:connecttype=\"rect\"/><o:lock v:ext=\"edit\" shapetype=\"t\"/></v:shapetype>");
        }

        if (commentRefs.Count > 0)
        {
            xml.Append("<v:shapetype id=\"_x0000_t202\" coordsize=\"21600,21600\" o:spt=\"202\" path=\"m,l,21600r21600,l21600,xe\"><v:stroke joinstyle=\"miter\"/><v:path gradientshapeok=\"t\" o:connecttype=\"rect\"/></v:shapetype>");
        }

        foreach (var control in controls)
        {
            var from = CellRef.Parse(control.From);
            var to = CellRef.Parse(control.To);
            var alt = control.AltText is null ? "" : $" alt=\"{FixtureXlsx.Esc(control.AltText)}\"";
            xml.Append(CultureInfo.InvariantCulture, $"<v:shape id=\"_x0000_s{control.ShapeId}\" type=\"#_x0000_t201\"{alt} style=\"position:absolute;margin-left:48pt;margin-top:15pt;width:96pt;height:18pt;z-index:1\" filled=\"f\" stroked=\"f\" o:insetmode=\"auto\">");
            if (!control.IsActiveX)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<v:textbox style=\"mso-direction-alt:auto\" o:singleclick=\"f\"><div style=\"text-align:left\">{FixtureXlsx.Esc(control.Label)}</div></v:textbox>");
            }

            var type = control.IsActiveX ? "Pict" : control.ObjectType == "CheckBox" ? "Checkbox" : control.ObjectType;
            xml.Append(CultureInfo.InvariantCulture, $"<x:ClientData ObjectType=\"{type}\"><x:SizeWithCells/><x:Anchor>{from.Column - 1}, 0, {from.Row - 1}, 0, {to.Column - 1}, 0, {to.Row - 1}, 0</x:Anchor><x:AutoFill>False</x:AutoFill><x:AutoLine>False</x:AutoLine>");
            if (control.LinkedCell is not null && !control.IsActiveX)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<x:FmlaLink>{FixtureXlsx.Esc(control.LinkedCell)}</x:FmlaLink>");
            }

            xml.Append("</x:ClientData></v:shape>");
        }

        var noteId = 2049;
        foreach (var reference in commentRefs)
        {
            var cell = CellRef.Parse(reference);
            xml.Append(CultureInfo.InvariantCulture, $"<v:shape id=\"_x0000_s{noteId++}\" type=\"#_x0000_t202\" style=\"position:absolute;margin-left:80pt;margin-top:5pt;width:108pt;height:60pt;z-index:1;visibility:hidden\" fillcolor=\"#ffffe1\" o:insetmode=\"auto\"><v:fill color2=\"#ffffe1\"/><v:shadow on=\"t\" color=\"black\" obscured=\"t\"/><v:path o:connecttype=\"none\"/><v:textbox style=\"mso-direction-alt:auto\"><div style=\"text-align:left\"></div></v:textbox>");
            xml.Append(CultureInfo.InvariantCulture, $"<x:ClientData ObjectType=\"Note\"><x:MoveWithCells/><x:SizeWithCells/><x:Anchor>{cell.Column}, 15, {cell.Row - 1}, 2, {cell.Column + 2}, 15, {cell.Row + 3}, 16</x:Anchor><x:AutoFill>False</x:AutoFill><x:Row>{cell.Row - 1}</x:Row><x:Column>{cell.Column - 1}</x:Column></x:ClientData></v:shape>");
        }

        xml.Append("</xml>");
        return xml.ToString();
    }

    private static string Marker(string name, string reference)
    {
        var cell = CellRef.Parse(reference);
        return string.Create(CultureInfo.InvariantCulture,
            $"<xdr:{name}><xdr:col>{cell.Column - 1}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{cell.Row - 1}</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:{name}>");
    }

    private static void Object(StringBuilder xml, FixtureXlsx owner, FxDrawingItem item, FixtureXlsx.Rels rels,
        SortedDictionary<string, byte[]> parts, SortedDictionary<string, string> overrides, SortedDictionary<string, string> defaults,
        FixtureXlsx.PartCounters counters)
    {
        var properties = new StringBuilder();
        properties.Append(CultureInfo.InvariantCulture, $"<xdr:cNvPr id=\"{item.Id}\" name=\"{FixtureXlsx.Esc(item.Name)}\"");
        if (item.Description is not null)
        {
            properties.Append(CultureInfo.InvariantCulture, $" descr=\"{FixtureXlsx.Esc(item.Description)}\"");
        }

        if (item.Hidden)
        {
            properties.Append(" hidden=\"1\"");
        }

        if (item.Title is not null)
        {
            properties.Append(CultureInfo.InvariantCulture, $" title=\"{FixtureXlsx.Esc(item.Title)}\"");
        }

        var inner = new StringBuilder();
        if (item.HyperlinkUrl is not null)
        {
            inner.Append(CultureInfo.InvariantCulture, $"<a:hlinkClick r:id=\"{rels.Add("hyperlink", item.HyperlinkUrl, external: true)}\"/>");
        }

        if (item.Decorative)
        {
            inner.Append(DecorativeExt);
        }

        properties.Append(inner.Length == 0 ? "/>" : ">" + inner + "</xdr:cNvPr>");
        const string Geometry = "<a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"952500\" cy=\"952500\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom>";

        switch (item.Kind)
        {
            case "pic":
                parts["xl/media/" + item.File] = FixtureXlsx.PixelPng;
                defaults["png"] = "image/png";
                var imageId = rels.Add("image", "../media/" + item.File);
                xml.Append("<xdr:pic><xdr:nvPicPr>").Append(properties).Append("<xdr:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></xdr:cNvPicPr></xdr:nvPicPr>");
                xml.Append(CultureInfo.InvariantCulture, $"<xdr:blipFill><a:blip r:embed=\"{imageId}\"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill><xdr:spPr>{Geometry}</xdr:spPr></xdr:pic>");
                break;
            case "sp":
                xml.Append("<xdr:sp macro=\"\" textlink=\"\"><xdr:nvSpPr>").Append(properties);
                xml.Append(item.TextBox ? "<xdr:cNvSpPr txBox=\"1\"/>" : "<xdr:cNvSpPr/>").Append("</xdr:nvSpPr>");
                xml.Append(CultureInfo.InvariantCulture, $"<xdr:spPr>{Geometry}<a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"000000\"/></a:solidFill></a:ln></xdr:spPr>");
                xml.Append("<xdr:txBody><a:bodyPr wrap=\"square\" rtlCol=\"0\" anchor=\"t\"/><a:lstStyle/>");
                foreach (var paragraph in (item.Text.Length == 0 ? [""] : item.Text.Split('\n')))
                {
                    xml.Append(paragraph.Length == 0
                        ? "<a:p><a:endParaRPr lang=\"pl-PL\"/></a:p>"
                        : $"<a:p><a:r><a:rPr lang=\"pl-PL\" sz=\"1100\"><a:solidFill><a:srgbClr val=\"000000\"/></a:solidFill></a:rPr><a:t>{FixtureXlsx.Esc(paragraph)}</a:t></a:r></a:p>");
                }

                xml.Append("</xdr:txBody></xdr:sp>");
                break;
            case "graphicFrame":
                var number = ++counters.Chart;
                parts[$"xl/charts/chart{number}.xml"] = new UTF8Encoding(false).GetBytes(item.ChartModel!.ToXml(owner));
                overrides[$"/xl/charts/chart{number}.xml"] = "application/vnd.openxmlformats-officedocument.drawingml.chart+xml";
                var chartId = rels.Add("chart", $"../charts/chart{number}.xml");
                xml.Append("<xdr:graphicFrame macro=\"\"><xdr:nvGraphicFramePr>").Append(properties).Append("<xdr:cNvGraphicFramePr/></xdr:nvGraphicFramePr>");
                xml.Append("<xdr:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"0\" cy=\"0\"/></xdr:xfrm>");
                xml.Append(CultureInfo.InvariantCulture, $"<a:graphic><a:graphicData uri=\"{owner.ChartNs}\"><c:chart xmlns:c=\"{owner.ChartNs}\" r:id=\"{chartId}\"/></a:graphicData></a:graphic></xdr:graphicFrame>");
                break;
            case "grpSp":
                xml.Append("<xdr:grpSp><xdr:nvGrpSpPr>").Append(properties).Append("<xdr:cNvGrpSpPr/></xdr:nvGrpSpPr>");
                xml.Append("<xdr:grpSpPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"1905000\" cy=\"952500\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"1905000\" cy=\"952500\"/></a:xfrm></xdr:grpSpPr>");
                foreach (var child in item.Children)
                {
                    Object(xml, owner, child, rels, parts, overrides, defaults, counters);
                }

                xml.Append("</xdr:grpSp>");
                break;
            default:
                throw new InvalidOperationException($"Nieznany rodzaj obiektu rysunku: {item.Kind}.");
        }
    }
}
