using System.Globalization;
using System.Text;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Fixtures.Xlsx;

/// <summary>Arkusz fixtury: komórki, struktura, tabele, łącza, obiekty rysunku, komentarze, kontrolki.</summary>
public sealed class FxSheet
{
    private const string X14Ns = "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main";
    private const string McNs = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    private readonly FixtureXlsx _owner;
    private readonly SortedDictionary<(int Row, int Column), FxCell> _cells = [];
    private readonly SortedDictionary<int, (bool Hidden, double? Height)> _rows = [];
    private readonly List<string> _columns = [];
    private readonly List<string> _merges = [];
    private readonly List<(string Ref, string? Url, string? Location, string? Display, string? Tooltip)> _hyperlinks = [];
    private readonly List<FxTable> _tables = [];
    private readonly List<FxDrawingItem> _drawings = [];
    private readonly List<(string Ref, string Author, string Text)> _comments = [];
    private readonly List<(string Ref, string PersonId, string Id, string Text)> _threaded = [];
    private readonly List<FxControl> _controls = [];

    internal FxSheet(FixtureXlsx owner, string name, string state)
    {
        _owner = owner;
        Name = name;
        State = state;
    }

    public string Name { get; }

    /// <summary>visible | hidden | veryHidden</summary>
    public string State { get; }

    public bool IsChartSheet { get; internal set; }

    /// <summary>Liczba zamrożonych wierszy u góry (okienko <c>pane</c>).</summary>
    public int FrozenRows { get; set; }

    public string? TopLeftCell { get; set; }

    /// <summary>Ochrona arkusza (<c>sheetProtection sheet="1"</c>).</summary>
    public bool Protected { get; set; }

    /// <summary>Surowe elementy <c>conditionalFormatting</c>.</summary>
    public List<string> ConditionalFormats { get; } = [];

    /// <summary>Surowe elementy <c>dataValidation</c>.</summary>
    public List<string> DataValidations { get; } = [];

    /// <summary>Surowa zawartość <c>extLst</c> arkusza (elementy <c>ext</c>).</summary>
    public List<string> Extensions { get; } = [];

    /// <summary>Dopisuje surowe wiersze za wierszami z <see cref="Text"/> itd. (duże arkusze); musi pisać rosnące numery wierszy.</summary>
    public Action<StringBuilder>? BulkRows { get; set; }

    /// <summary>Tekst jako tekst wspólny (<c>t="s"</c>).</summary>
    public FxSheet Text(string reference, string text, int style = 0) =>
        Set(reference, new FxCell("s", _owner.SharedString(FixtureXlsx.T(text)).ToString(CultureInfo.InvariantCulture), style));

    /// <summary>Tekst wspólny z formatowaniem fragmentów – surowe wnętrze <c>si</c> (elementy <c>r</c>).</summary>
    public FxSheet Rich(string reference, string siInnerXml, int style = 0) =>
        Set(reference, new FxCell("s", _owner.SharedString(siInnerXml).ToString(CultureInfo.InvariantCulture), style));

    /// <summary>Tekst wbudowany (<c>t="inlineStr"</c>); <paramref name="isInnerXml"/> pozwala podać surowe wnętrze <c>is</c>.</summary>
    public FxSheet Inline(string reference, string text, int style = 0, string? isInnerXml = null) =>
        Set(reference, new FxCell("inlineStr", null, style) { InlineXml = isInnerXml ?? FixtureXlsx.T(text) });

    public FxSheet Number(string reference, double value, int style = 0) =>
        Set(reference, new FxCell(null, value.ToString("R", CultureInfo.InvariantCulture), style));

    public FxSheet Bool(string reference, bool value, int style = 0) => Set(reference, new FxCell("b", value ? "1" : "0", style));

    public FxSheet Error(string reference, string error, int style = 0) => Set(reference, new FxCell("e", error, style));

    /// <summary>Formuła z wartością z pamięci podręcznej (<paramref name="cached"/> = null: brak wartości); <paramref name="type"/>: null (liczba), „str”, „b”, „e”.</summary>
    public FxSheet Formula(string reference, string formula, string? cached, string? type = null, int style = 0) =>
        Set(reference, new FxCell(type, cached, style) { Formula = formula });

    /// <summary>Komórka bez wartości, z samym stylem.</summary>
    public FxSheet Styled(string reference, int style) => Set(reference, new FxCell(null, null, style));

    /// <summary>Wiersz tekstów od podanej komórki w prawo.</summary>
    public FxSheet Row(string start, int style, params string[] texts)
    {
        var first = CellRef.Parse(start);
        for (var i = 0; i < texts.Length; i++)
        {
            Text(new CellRef(first.Row, first.Column + i).ToString(), texts[i], style);
        }

        return this;
    }

    public FxSheet HideRow(int row)
    {
        _rows[row] = (true, _rows.TryGetValue(row, out var existing) ? existing.Height : null);
        return this;
    }

    public FxSheet RowHeight(int row, double height)
    {
        _rows[row] = (_rows.TryGetValue(row, out var existing) && existing.Hidden, height);
        return this;
    }

    /// <summary>Zakres kolumn (od 1): szerokość, ukrycie.</summary>
    public FxSheet Columns(int min, int max, double width = 12, bool hidden = false)
    {
        _columns.Add($"<col min=\"{min}\" max=\"{max}\" width=\"{width.ToString(CultureInfo.InvariantCulture)}\"{(hidden ? " hidden=\"1\"" : "")} customWidth=\"1\"/>");
        return this;
    }

    public FxSheet Merge(string range)
    {
        _merges.Add(range);
        return this;
    }

    /// <summary>Łącze zewnętrzne (<paramref name="url"/>) albo wewnętrzne (<paramref name="location"/>, np. „'Arkusz'!A1”).</summary>
    public FxSheet Hyperlink(string reference, string? url = null, string? location = null, string? display = null, string? tooltip = null)
    {
        _hyperlinks.Add((reference, url, location, display, tooltip));
        return this;
    }

    public FxSheet Table(FxTable table)
    {
        _tables.Add(table);
        return this;
    }

    public FxSheet Image(string name, int id, string from, string to, string? description = null, bool decorative = false,
        string? title = null, string file = "image1.png", string? hyperlink = null, bool hidden = false)
    {
        _drawings.Add(FxDrawingItem.Picture(name, id, description, decorative, title, file, hyperlink, hidden).At(from, to));
        return this;
    }

    public FxSheet Shape(string name, int id, string from, string to, string text = "", bool textBox = false, string? description = null,
        bool decorative = false, string? hyperlink = null)
    {
        _drawings.Add(FxDrawingItem.Shape(name, id, text, textBox, description, decorative, hyperlink).At(from, to));
        return this;
    }

    public FxSheet Chart(FxChart chart, string name, int id, string from, string to, string? description = null, string? title = null)
    {
        _drawings.Add(new FxDrawingItem { Kind = "graphicFrame", Name = name, Id = id, Description = description, Title = title, ChartModel = chart }.At(from, to));
        return this;
    }

    public FxSheet Group(string name, int id, string from, string to, string? description, params FxDrawingItem[] children)
    {
        var group = new FxDrawingItem { Kind = "grpSp", Name = name, Id = id, Description = description }.At(from, to);
        group.Children.AddRange(children);
        _drawings.Add(group);
        return this;
    }

    public FxSheet Comment(string reference, string author, string text)
    {
        _comments.Add((reference, author, text));
        return this;
    }

    /// <summary>Komentarz wątkowy; Excel zapisuje do niego także zaślepkę w notatkach (autor „tc={id}”) – generator robi to samo.</summary>
    public FxSheet ThreadedComment(string reference, string personId, string id, string text)
    {
        _threaded.Add((reference, personId, id, text));
        return this;
    }

    /// <summary>Kontrolka formularza: <paramref name="objectType"/> jak w <c>formControlPr</c> (CheckBox, Button, Drop, Radio…).</summary>
    public FxSheet FormControl(string name, int shapeId, string objectType, string from, string to, string? altText = null, string? label = null,
        string? linkedCell = null)
    {
        _controls.Add(new FxControl(name, shapeId, objectType, from, to, altText, label, linkedCell, false));
        return this;
    }

    public FxSheet ActiveX(string name, int shapeId, string from, string to, string? altText = null)
    {
        _controls.Add(new FxControl(name, shapeId, "Pict", from, to, altText, null, null, true));
        return this;
    }

    private FxSheet Set(string reference, FxCell cell)
    {
        var parsed = CellRef.Parse(reference);
        _cells[(parsed.Row, parsed.Column)] = cell;
        return this;
    }

    internal void Write(string partName, SortedDictionary<string, byte[]> parts, SortedDictionary<string, string> overrides,
        SortedDictionary<string, string> defaults, FixtureXlsx.PartCounters counters)
    {
        var utf8 = new UTF8Encoding(false);
        var rels = new FixtureXlsx.Rels(_owner);
        var xml = new StringBuilder();
        var hasControls = _controls.Count > 0;

        // Rysunek: obiekty arkusza i kształty kontrolek.
        string? drawingRelId = null;
        if (_drawings.Count > 0 || hasControls)
        {
            var number = ++counters.Drawing;
            var drawingPart = $"xl/drawings/drawing{number}.xml";
            var drawingXml = FxDrawingWriter.Drawing(_owner, _drawings, _controls, IsChartSheet, parts, overrides, defaults, counters, out var drawingRels);
            parts[drawingPart] = utf8.GetBytes(drawingXml);
            overrides["/" + drawingPart] = "application/vnd.openxmlformats-officedocument.drawing+xml";
            if (drawingRels.Any)
            {
                parts[$"xl/drawings/_rels/drawing{number}.xml.rels"] = utf8.GetBytes(drawingRels.ToXml());
            }

            drawingRelId = rels.Add("drawing", $"../drawings/drawing{number}.xml");
        }

        if (IsChartSheet)
        {
            xml.Append(CultureInfo.InvariantCulture, $"{FixtureXlsx.XmlHeader}<chartsheet xmlns=\"{_owner.MainNs}\" xmlns:r=\"{_owner.RelNs}\">");
            xml.Append("<sheetViews><sheetView workbookViewId=\"0\" zoomToFit=\"1\"/></sheetViews>");
            xml.Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
            xml.Append(CultureInfo.InvariantCulture, $"<drawing r:id=\"{drawingRelId}\"/></chartsheet>");
            parts[partName] = utf8.GetBytes(xml.ToString());
            overrides["/" + partName] = "application/vnd.openxmlformats-officedocument.spreadsheetml.chartsheet+xml";
            parts[RelsName(partName)] = utf8.GetBytes(rels.ToXml());
            return;
        }

        xml.Append(CultureInfo.InvariantCulture, $"{FixtureXlsx.XmlHeader}<worksheet xmlns=\"{_owner.MainNs}\" xmlns:r=\"{_owner.RelNs}\"");
        if (hasControls)
        {
            xml.Append(CultureInfo.InvariantCulture, $" xmlns:mc=\"{McNs}\" xmlns:x14=\"{X14Ns}\" xmlns:xdr=\"{_owner.SheetDrawingNs}\" mc:Ignorable=\"x14\"");
        }

        xml.Append('>');
        xml.Append("<sheetViews><sheetView workbookViewId=\"0\"");
        if (TopLeftCell is not null)
        {
            xml.Append(CultureInfo.InvariantCulture, $" topLeftCell=\"{TopLeftCell}\"");
        }

        if (FrozenRows > 0)
        {
            xml.Append(CultureInfo.InvariantCulture, $"><pane ySplit=\"{FrozenRows}\" topLeftCell=\"A{FrozenRows + 1}\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        }
        else
        {
            xml.Append("/></sheetViews>");
        }

        xml.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
        if (_columns.Count > 0)
        {
            xml.Append("<cols>").AppendJoin("", _columns).Append("</cols>");
        }

        WriteRows(xml);

        if (Protected)
        {
            xml.Append("<sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\"/>");
        }

        if (_merges.Count > 0)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<mergeCells count=\"{_merges.Count}\">");
            foreach (var merge in _merges)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<mergeCell ref=\"{merge}\"/>");
            }

            xml.Append("</mergeCells>");
        }

        xml.AppendJoin("", ConditionalFormats);
        if (DataValidations.Count > 0)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<dataValidations count=\"{DataValidations.Count}\">").AppendJoin("", DataValidations).Append("</dataValidations>");
        }

        if (_hyperlinks.Count > 0)
        {
            xml.Append("<hyperlinks>");
            foreach (var (reference, url, location, display, tooltip) in _hyperlinks)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<hyperlink ref=\"{reference}\"");
                if (url is not null)
                {
                    xml.Append(CultureInfo.InvariantCulture, $" r:id=\"{rels.Add("hyperlink", url, external: true)}\"");
                }

                if (location is not null)
                {
                    xml.Append(CultureInfo.InvariantCulture, $" location=\"{FixtureXlsx.Esc(location)}\"");
                }

                if (tooltip is not null)
                {
                    xml.Append(CultureInfo.InvariantCulture, $" tooltip=\"{FixtureXlsx.Esc(tooltip)}\"");
                }

                if (display is not null)
                {
                    xml.Append(CultureInfo.InvariantCulture, $" display=\"{FixtureXlsx.Esc(display)}\"");
                }

                xml.Append("/>");
            }

            xml.Append("</hyperlinks>");
        }

        xml.Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
        if (drawingRelId is not null)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<drawing r:id=\"{drawingRelId}\"/>");
        }

        // Notatki, zaślepki komentarzy wątkowych i kontrolki potrzebują starego rysunku VML.
        if (_comments.Count > 0 || _threaded.Count > 0 || hasControls)
        {
            var number = ++counters.Vml;
            parts[$"xl/drawings/vmlDrawing{number}.vml"] = utf8.GetBytes(FxDrawingWriter.Vml(_comments.Select(c => c.Ref).Concat(_threaded.Select(t => t.Ref)).ToList(), _controls));
            defaults["vml"] = "application/vnd.openxmlformats-officedocument.vmlDrawing";
            xml.Append(CultureInfo.InvariantCulture, $"<legacyDrawing r:id=\"{rels.Add("vmlDrawing", $"../drawings/vmlDrawing{number}.vml")}\"/>");
        }

        if (hasControls)
        {
            WriteControls(xml, rels, parts, overrides, defaults, counters);
        }

        if (_tables.Count > 0)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<tableParts count=\"{_tables.Count}\">");
            foreach (var table in _tables)
            {
                var number = ++counters.Table;
                var tablePart = $"xl/tables/table{number}.xml";
                parts[tablePart] = utf8.GetBytes(table.ToXml(_owner.MainNs, number));
                overrides["/" + tablePart] = "application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml";
                xml.Append(CultureInfo.InvariantCulture, $"<tablePart r:id=\"{rels.Add("table", $"../tables/table{number}.xml")}\"/>");
            }

            xml.Append("</tableParts>");
        }

        if (Extensions.Count > 0)
        {
            xml.Append("<extLst>").AppendJoin("", Extensions).Append("</extLst>");
        }

        xml.Append("</worksheet>");

        if (_comments.Count > 0 || _threaded.Count > 0)
        {
            var number = ++counters.Comments;
            var authors = _comments.Select(c => c.Author).Concat(_threaded.Select(t => "tc=" + t.Id)).Distinct().ToList();
            var comments = new StringBuilder();
            comments.Append(CultureInfo.InvariantCulture, $"{FixtureXlsx.XmlHeader}<comments xmlns=\"{_owner.MainNs}\"><authors>");
            foreach (var author in authors)
            {
                comments.Append(CultureInfo.InvariantCulture, $"<author>{FixtureXlsx.Esc(author)}</author>");
            }

            comments.Append("</authors><commentList>");
            var all = _comments.Select(c => (c.Ref, Author: c.Author, c.Text))
                .Concat(_threaded.Select(t => (t.Ref, Author: "tc=" + t.Id, Text: "[Komentarz z wątkiem] Twoja wersja programu Excel pozwala odczytać ten komentarz z wątkiem, ale jego edycje zostaną usunięte. Komentarz: " + t.Text)))
                .OrderBy(c => CellRef.Parse(c.Ref));
            foreach (var (reference, author, text) in all)
            {
                comments.Append(CultureInfo.InvariantCulture, $"<comment ref=\"{reference}\" authorId=\"{authors.IndexOf(author)}\"><text>{FixtureXlsx.T(text)}</text></comment>");
            }

            comments.Append("</commentList></comments>");
            parts[$"xl/comments{number}.xml"] = utf8.GetBytes(comments.ToString());
            overrides[$"/xl/comments{number}.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.comments+xml";
            rels.Add("comments", $"../comments{number}.xml");
        }

        if (_threaded.Count > 0)
        {
            var number = ++counters.Threaded;
            var threaded = new StringBuilder();
            threaded.Append(FixtureXlsx.XmlHeader).Append("<ThreadedComments xmlns=\"http://schemas.microsoft.com/office/spreadsheetml/2018/threadedcomments\">");
            foreach (var (reference, personId, id, text) in _threaded)
            {
                threaded.Append(CultureInfo.InvariantCulture, $"<threadedComment ref=\"{reference}\" dT=\"2020-01-01T00:00:00.00\" personId=\"{personId}\" id=\"{id}\"><text>{FixtureXlsx.Esc(text)}</text></threadedComment>");
            }

            threaded.Append("</ThreadedComments>");
            parts[$"xl/threadedComments/threadedComment{number}.xml"] = utf8.GetBytes(threaded.ToString());
            overrides[$"/xl/threadedComments/threadedComment{number}.xml"] = "application/vnd.ms-excel.threadedcomments+xml";
            rels.AddAbsoluteType("http://schemas.microsoft.com/office/2017/10/relationships/threadedComment", $"../threadedComments/threadedComment{number}.xml");
        }

        parts[partName] = utf8.GetBytes(xml.ToString());
        overrides["/" + partName] = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
        if (rels.Any)
        {
            parts[RelsName(partName)] = utf8.GetBytes(rels.ToXml());
        }
    }

    private static string RelsName(string partName)
    {
        var slash = partName.LastIndexOf('/');
        return $"{partName[..slash]}/_rels/{partName[(slash + 1)..]}.rels";
    }

    private void WriteRows(StringBuilder xml)
    {
        if (_cells.Count == 0 && _rows.Count == 0 && BulkRows is null)
        {
            xml.Append("<sheetData/>");
            return;
        }

        xml.Append("<sheetData>");
        var rowNumbers = new SortedSet<int>(_cells.Keys.Select(k => k.Row));
        rowNumbers.UnionWith(_rows.Keys);
        using var cells = _cells.GetEnumerator();
        var hasCell = cells.MoveNext();
        foreach (var row in rowNumbers)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{row}\"");
            if (_rows.TryGetValue(row, out var info))
            {
                if (info.Height is { } height)
                {
                    xml.Append(CultureInfo.InvariantCulture, $" ht=\"{height.ToString(CultureInfo.InvariantCulture)}\" customHeight=\"1\"");
                }

                if (info.Hidden)
                {
                    xml.Append(" hidden=\"1\"");
                }
            }

            xml.Append('>');
            while (hasCell && cells.Current.Key.Row == row)
            {
                var ((_, column), cell) = cells.Current;
                xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{new CellRef(row, column)}\"");
                if (cell.Style != 0)
                {
                    xml.Append(CultureInfo.InvariantCulture, $" s=\"{cell.Style}\"");
                }

                if (cell.Type is not null)
                {
                    xml.Append(CultureInfo.InvariantCulture, $" t=\"{cell.Type}\"");
                }

                if (cell.Formula is null && cell.Value is null && cell.InlineXml is null)
                {
                    xml.Append("/>");
                }
                else
                {
                    xml.Append('>');
                    if (cell.Formula is not null)
                    {
                        xml.Append("<f>").Append(FixtureXlsx.Esc(cell.Formula)).Append("</f>");
                    }

                    if (cell.Value is not null)
                    {
                        xml.Append("<v>").Append(FixtureXlsx.Esc(cell.Value)).Append("</v>");
                    }

                    if (cell.InlineXml is not null)
                    {
                        xml.Append("<is>").Append(cell.InlineXml).Append("</is>");
                    }

                    xml.Append("</c>");
                }

                hasCell = cells.MoveNext();
            }

            xml.Append("</row>");
        }

        BulkRows?.Invoke(xml);
        xml.Append("</sheetData>");
    }

    private void WriteControls(StringBuilder xml, FixtureXlsx.Rels rels, SortedDictionary<string, byte[]> parts,
        SortedDictionary<string, string> overrides, SortedDictionary<string, string> defaults, FixtureXlsx.PartCounters counters)
    {
        var utf8 = new UTF8Encoding(false);
        xml.Append("<mc:AlternateContent><mc:Choice Requires=\"x14\"><controls>");
        foreach (var control in _controls)
        {
            string relId;
            if (control.IsActiveX)
            {
                var number = ++counters.ActiveX;
                var activeXRels = new FixtureXlsx.Rels(_owner);
                var binaryId = activeXRels.AddAbsoluteType("http://schemas.microsoft.com/office/2006/relationships/activeXControlBinary", $"activeX{number}.bin");
                parts[$"xl/activeX/activeX{number}.xml"] = utf8.GetBytes(
                    $"{FixtureXlsx.XmlHeader}<ax:ocx ax:classid=\"{{D7053240-CE69-11CD-A777-00DD01143C57}}\" ax:persistence=\"persistStreamInit\" r:id=\"{binaryId}\" xmlns:ax=\"http://schemas.microsoft.com/office/2006/activeX\" xmlns:r=\"{_owner.RelNs}\"/>");
                overrides[$"/xl/activeX/activeX{number}.xml"] = "application/vnd.ms-office.activeX+xml";
                parts[$"xl/activeX/activeX{number}.bin"] = utf8.GetBytes("ATRAPA activeX.bin - fixtura testowa");
                parts[$"xl/activeX/_rels/activeX{number}.xml.rels"] = utf8.GetBytes(activeXRels.ToXml());
                defaults["bin"] = "application/vnd.ms-office.activeX";
                relId = rels.Add("control", $"../activeX/activeX{number}.xml");
            }
            else
            {
                var number = ++counters.CtrlProp;
                var link = control.LinkedCell is null ? "" : $" fmlaLink=\"{FixtureXlsx.Esc(control.LinkedCell)}\"";
                parts[$"xl/ctrlProps/ctrlProp{number}.xml"] = utf8.GetBytes(
                    $"{FixtureXlsx.XmlHeader}<formControlPr xmlns=\"{X14Ns}\" objectType=\"{control.ObjectType}\"{link} lockText=\"1\" noThreeD=\"1\"/>");
                overrides[$"/xl/ctrlProps/ctrlProp{number}.xml"] = "application/vnd.ms-excel.controlproperties+xml";
                relId = rels.Add("ctrlProp", $"../ctrlProps/ctrlProp{number}.xml");
            }

            var from = CellRef.Parse(control.From);
            var to = CellRef.Parse(control.To);
            var alt = control.AltText is null ? "" : $" altText=\"{FixtureXlsx.Esc(control.AltText)}\"";
            var linked = control.IsActiveX || control.LinkedCell is null ? "" : $" linkedCell=\"{FixtureXlsx.Esc(control.LinkedCell)}\"";
            var name = FixtureXlsx.Esc(control.Name);
            xml.Append("<mc:AlternateContent><mc:Choice Requires=\"x14\">");
            xml.Append(CultureInfo.InvariantCulture, $"<control shapeId=\"{control.ShapeId}\" r:id=\"{relId}\" name=\"{name}\">");
            xml.Append(CultureInfo.InvariantCulture, $"<controlPr defaultSize=\"0\" autoFill=\"0\" autoLine=\"0\" autoPict=\"0\"{alt}{linked}><anchor moveWithCells=\"1\">");
            xml.Append(CultureInfo.InvariantCulture, $"<from><xdr:col>{from.Column - 1}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{from.Row - 1}</xdr:row><xdr:rowOff>0</xdr:rowOff></from>");
            xml.Append(CultureInfo.InvariantCulture, $"<to><xdr:col>{to.Column - 1}</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>{to.Row - 1}</xdr:row><xdr:rowOff>0</xdr:rowOff></to>");
            xml.Append("</anchor></controlPr></control></mc:Choice>");
            if (control.IsActiveX)
            {
                // Gałąź zgodności powtarza kontrolkę – parser nie może policzyć jej dwa razy.
                xml.Append(CultureInfo.InvariantCulture, $"<mc:Fallback><control shapeId=\"{control.ShapeId}\" r:id=\"{relId}\" name=\"{name}\"/></mc:Fallback>");
            }

            xml.Append("</mc:AlternateContent>");
        }

        xml.Append("</controls></mc:Choice></mc:AlternateContent>");
    }

    private sealed record FxCell(string? Type, string? Value, int Style)
    {
        public string? Formula { get; init; }

        public string? InlineXml { get; init; }
    }
}

internal sealed record FxControl(string Name, int ShapeId, string ObjectType, string From, string To, string? AltText, string? Label,
    string? LinkedCell, bool IsActiveX);

/// <summary>Tabela Excela (<c>xl/tables/tableN.xml</c>).</summary>
public sealed record FxTable(string Name, string Ref, string[] Columns)
{
    public bool HeaderRow { get; init; } = true;

    /// <summary>Nazwa stylu tabeli; <c>null</c> = bez stylu.</summary>
    public string? Style { get; init; } = "TableStyleLight1";

    public string? AltText { get; init; }

    public string? AltTextSummary { get; init; }

    internal string ToXml(string mainNs, int id)
    {
        var xml = new StringBuilder();
        xml.Append(CultureInfo.InvariantCulture, $"{FixtureXlsx.XmlHeader}<table xmlns=\"{mainNs}\" id=\"{id}\" name=\"{FixtureXlsx.Esc(Name)}\" displayName=\"{FixtureXlsx.Esc(Name)}\" ref=\"{Ref}\"");
        xml.Append(HeaderRow ? " totalsRowShown=\"0\">" : " headerRowCount=\"0\" totalsRowShown=\"0\">");
        if (HeaderRow)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<autoFilter ref=\"{Ref}\"/>");
        }

        xml.Append(CultureInfo.InvariantCulture, $"<tableColumns count=\"{Columns.Length}\">");
        for (var i = 0; i < Columns.Length; i++)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<tableColumn id=\"{i + 1}\" name=\"{FixtureXlsx.Esc(Columns[i])}\"/>");
        }

        xml.Append("</tableColumns>");
        if (Style is not null)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<tableStyleInfo name=\"{Style}\" showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"1\" showColumnStripes=\"0\"/>");
        }

        if (AltText is not null || AltTextSummary is not null)
        {
            xml.Append("<extLst><ext uri=\"{504A1905-F514-4f6f-8877-14C23A59335A}\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\"><x14:table");
            if (AltText is not null)
            {
                xml.Append(CultureInfo.InvariantCulture, $" altText=\"{FixtureXlsx.Esc(AltText)}\"");
            }

            if (AltTextSummary is not null)
            {
                xml.Append(CultureInfo.InvariantCulture, $" altTextSummary=\"{FixtureXlsx.Esc(AltTextSummary)}\"");
            }

            xml.Append("/></ext></extLst>");
        }

        xml.Append("</table>");
        return xml.ToString();
    }
}
