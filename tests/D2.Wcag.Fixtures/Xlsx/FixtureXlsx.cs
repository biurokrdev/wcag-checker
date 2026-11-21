using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace D2.Wcag.Fixtures.Xlsx;

// Deterministyczny generator fixtur XLSX: części pakietu składane są jako tekst XML (bez Open XML SDK), dzięki czemu
// generator kontroluje każdy bajt części, potrafi pisać Strict OOXML i celowo „zepsute” konstrukcje.
// Determinizm: jawne identyfikatory relacji (rId1…), stała kolejność i stała data wpisów ZIP, żadnych identyfikatorów losowych.
public sealed partial class FixtureXlsx
{
    public const string Company = "Example Company S. A.";

    private static readonly DateTimeOffset ZipTimestamp = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>PNG 1 × 1 (pomarańczowy piksel) – treść każdego obrazu w fixturach.</summary>
    internal static readonly byte[] PixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");

    private readonly List<string> _sharedStrings = [];
    private readonly Dictionary<string, int> _sharedIndex = new(StringComparer.Ordinal);

    /// <summary>Strict Open XML: przestrzenie nazw <c>purl.oclc.org</c> i <c>conformance="strict"</c>.</summary>
    public bool Strict { get; set; }

    /// <summary>Pakiet .xlsm: typ części głównej „z makrami” i atrapa <c>xl/vbaProject.bin</c>.</summary>
    public bool MacroEnabled { get; set; }

    public string? Title { get; set; }

    public string? Subject { get; set; }

    public string? Creator { get; set; } = Company;

    public string? Language { get; set; }

    public string? Description { get; set; }

    public string? Keywords { get; set; }

    public string Application { get; set; } = "Microsoft Excel";

    public FxStyles Styles { get; } = new();

    public List<FxSheet> Sheets { get; } = [];

    /// <summary>Nazwy zdefiniowane: (nazwa, wartość, indeks arkusza lokalnego albo null, ukryta).</summary>
    public List<(string Name, string Value, int? LocalSheetId, bool Hidden)> DefinedNames { get; } = [];

    /// <summary>Osoby komentarzy wątkowych: (identyfikator GUID w klamrach, nazwa wyświetlana).</summary>
    public List<(string Id, string DisplayName)> Persons { get; } = [];

    /// <summary>Dodatkowe części pakietu (np. atrapa pliku audio), nazwa → (bajty, typ zawartości albo null, gdy wystarczy typ domyślny rozszerzenia).</summary>
    public Dictionary<string, (byte[] Content, string? ContentType)> ExtraParts { get; } = new(StringComparer.Ordinal);

    // Przestrzenie nazw zależne od odmiany (Transitional / Strict).
    internal string MainNs => Strict ? "http://purl.oclc.org/ooxml/spreadsheetml/main" : "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    internal string RelNs => Strict ? "http://purl.oclc.org/ooxml/officeDocument/relationships" : "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal string DrawingNs => Strict ? "http://purl.oclc.org/ooxml/drawingml/main" : "http://schemas.openxmlformats.org/drawingml/2006/main";

    internal string SheetDrawingNs => Strict ? "http://purl.oclc.org/ooxml/drawingml/spreadsheetDrawing" : "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";

    internal string ChartNs => Strict ? "http://purl.oclc.org/ooxml/drawingml/chart" : "http://schemas.openxmlformats.org/drawingml/2006/chart";

    internal string RelType(string name) => RelNs + "/" + name;

    public FxSheet AddSheet(string name, string state = "visible")
    {
        var sheet = new FxSheet(this, name, state);
        Sheets.Add(sheet);
        return sheet;
    }

    /// <summary>Arkusz wykresu (chartsheet) z jednym wykresem.</summary>
    public FxSheet AddChartSheet(string name, FxChart chart, string frameName = "Wykres 1", string? description = null)
    {
        var sheet = new FxSheet(this, name, "visible") { IsChartSheet = true };
        sheet.Chart(chart, frameName, 2, "A1", "J20", description);
        Sheets.Add(sheet);
        return sheet;
    }

    internal int SharedString(string siInnerXml)
    {
        if (!_sharedIndex.TryGetValue(siInnerXml, out var index))
        {
            index = _sharedStrings.Count;
            _sharedStrings.Add(siInnerXml);
            _sharedIndex[siInnerXml] = index;
        }

        return index;
    }

    /// <summary>Indeks zwykłego tekstu w tablicy tekstów wspólnych (do wierszy pisanych przez <see cref="FxSheet.BulkRows"/>).</summary>
    public int SharedStringIndex(string text) => SharedString(T(text));

    /// <summary>Części pakietu: nazwa → bajty (po rozpakowaniu). Do porównań niezależnych od kompresji.</summary>
    public static IReadOnlyDictionary<string, byte[]> Parts(byte[] package)
    {
        var parts = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        using var archive = new ZipArchive(new MemoryStream(package, writable: false), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            parts[entry.FullName] = buffer.ToArray();
        }

        return parts;
    }

    public byte[] Build()
    {
        var parts = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var overrides = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var defaults = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["rels"] = "application/vnd.openxmlformats-package.relationships+xml",
            ["xml"] = "application/xml",
        };
        var counters = new PartCounters();

        void Add(string name, string xml, string? contentType = null)
        {
            parts[name] = Utf8.GetBytes(xml);
            if (contentType is not null)
            {
                overrides["/" + name] = contentType;
            }
        }

        var workbookRels = new Rels(this);
        var sheetsXml = new StringBuilder();
        for (var i = 0; i < Sheets.Count; i++)
        {
            var sheet = Sheets[i];
            var partName = sheet.IsChartSheet ? $"xl/chartsheets/sheet{i + 1}.xml" : $"xl/worksheets/sheet{i + 1}.xml";
            var relId = workbookRels.Add(sheet.IsChartSheet ? "chartsheet" : "worksheet", partName["xl/".Length..]);
            sheetsXml.Append(CultureInfo.InvariantCulture, $"<sheet name=\"{Esc(sheet.Name)}\" sheetId=\"{i + 1}\"");
            if (sheet.State != "visible")
            {
                sheetsXml.Append(CultureInfo.InvariantCulture, $" state=\"{sheet.State}\"");
            }

            sheetsXml.Append(CultureInfo.InvariantCulture, $" r:id=\"{relId}\"/>");
            sheet.Write(partName, parts, overrides, defaults, counters);
        }

        workbookRels.Add("theme", "theme/theme1.xml");
        workbookRels.Add("styles", "styles.xml");
        if (_sharedStrings.Count > 0)
        {
            workbookRels.Add("sharedStrings", "sharedStrings.xml");
            var shared = new StringBuilder();
            shared.Append(CultureInfo.InvariantCulture, $"{XmlHeader}<sst xmlns=\"{MainNs}\" count=\"{_sharedStrings.Count}\" uniqueCount=\"{_sharedStrings.Count}\">");
            foreach (var item in _sharedStrings)
            {
                shared.Append("<si>").Append(item).Append("</si>");
            }

            shared.Append("</sst>");
            Add("xl/sharedStrings.xml", shared.ToString(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml");
        }

        if (Persons.Count > 0)
        {
            workbookRels.AddAbsoluteType("http://schemas.microsoft.com/office/2017/10/relationships/person", "persons/person.xml");
            var persons = new StringBuilder();
            persons.Append(XmlHeader).Append("<personList xmlns=\"http://schemas.microsoft.com/office/spreadsheetml/2018/threadedcomments\">");
            foreach (var (id, displayName) in Persons)
            {
                persons.Append(CultureInfo.InvariantCulture, $"<person displayName=\"{Esc(displayName)}\" id=\"{id}\" userId=\"{Esc(displayName)}\" providerId=\"None\"/>");
            }

            persons.Append("</personList>");
            Add("xl/persons/person.xml", persons.ToString(), "application/vnd.ms-excel.person+xml");
        }

        if (MacroEnabled)
        {
            workbookRels.AddAbsoluteType("http://schemas.microsoft.com/office/2006/relationships/vbaProject", "vbaProject.bin");
            // Atrapa: to nie jest prawdziwy projekt VBA i nigdy nie jest wykonywana.
            parts["xl/vbaProject.bin"] = Utf8.GetBytes("ATRAPA vbaProject.bin - fixtura testowa, bez kodu VBA");
            defaults["bin"] = "application/vnd.ms-office.vbaProject";
        }

        var workbook = new StringBuilder();
        workbook.Append(CultureInfo.InvariantCulture, $"{XmlHeader}<workbook xmlns=\"{MainNs}\" xmlns:r=\"{RelNs}\"");
        if (Strict)
        {
            workbook.Append(" conformance=\"strict\"");
        }

        workbook.Append("><bookViews><workbookView xWindow=\"0\" yWindow=\"0\" windowWidth=\"28800\" windowHeight=\"12300\"/></bookViews>");
        workbook.Append("<sheets>").Append(sheetsXml).Append("</sheets>");
        if (DefinedNames.Count > 0)
        {
            workbook.Append("<definedNames>");
            foreach (var (name, value, localSheetId, hidden) in DefinedNames)
            {
                workbook.Append(CultureInfo.InvariantCulture, $"<definedName name=\"{Esc(name)}\"");
                if (localSheetId is not null)
                {
                    workbook.Append(CultureInfo.InvariantCulture, $" localSheetId=\"{localSheetId}\"");
                }

                if (hidden)
                {
                    workbook.Append(" hidden=\"1\"");
                }

                workbook.Append('>').Append(Esc(value)).Append("</definedName>");
            }

            workbook.Append("</definedNames>");
        }

        workbook.Append("</workbook>");
        Add("xl/workbook.xml", workbook.ToString(), MacroEnabled
            ? "application/vnd.ms-excel.sheet.macroEnabled.main+xml"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        Add("xl/_rels/workbook.xml.rels", workbookRels.ToXml());
        Add("xl/styles.xml", Styles.ToXml(MainNs), "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        Add("xl/theme/theme1.xml", ThemeXml(), "application/vnd.openxmlformats-officedocument.theme+xml");

        Add("docProps/core.xml", CoreXml(), "application/vnd.openxmlformats-package.core-properties+xml");
        Add("docProps/app.xml",
            $"{XmlHeader}<Properties xmlns=\"{(Strict ? "http://purl.oclc.org/ooxml/officeDocument/extendedProperties" : "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties")}\"><Application>{Esc(Application)}</Application></Properties>",
            "application/vnd.openxmlformats-officedocument.extended-properties+xml");

        var rootRels = new Rels(this);
        rootRels.Add("officeDocument", "xl/workbook.xml");
        rootRels.AddAbsoluteType("http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
        rootRels.Add(Strict ? "extendedProperties" : "extended-properties", "docProps/app.xml");
        Add("_rels/.rels", rootRels.ToXml());

        foreach (var (name, (content, contentType)) in ExtraParts)
        {
            parts[name] = content;
            if (contentType is not null)
            {
                var extension = Path.GetExtension(name).TrimStart('.');
                if (contentType.StartsWith('.'))
                {
                    defaults[extension] = contentType[1..];
                }
                else
                {
                    overrides["/" + name] = contentType;
                }
            }
        }

        var types = new StringBuilder();
        types.Append(XmlHeader).Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        foreach (var (extension, contentType) in defaults)
        {
            types.Append(CultureInfo.InvariantCulture, $"<Default Extension=\"{extension}\" ContentType=\"{contentType}\"/>");
        }

        foreach (var (partName, contentType) in overrides)
        {
            types.Append(CultureInfo.InvariantCulture, $"<Override PartName=\"{partName}\" ContentType=\"{contentType}\"/>");
        }

        types.Append("</Types>");

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", Utf8.GetBytes(types.ToString()));
            foreach (var (name, content) in parts)
            {
                Write(archive, name, content);
            }
        }

        return output.ToArray();
    }

    internal const string XmlHeader = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n";

    internal static string Esc(string? text) => (text ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>Element <c>t</c> tekstu wspólnego / wbudowanego; spacje na brzegach wymagają <c>xml:space="preserve"</c>.</summary>
    internal static string T(string text) =>
        text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]) || text.Contains('\n'))
            ? $"<t xml:space=\"preserve\">{Esc(text)}</t>"
            : $"<t>{Esc(text)}</t>";

    private static void Write(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = ZipTimestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }

    private string CoreXml()
    {
        var xml = new StringBuilder();
        xml.Append(XmlHeader).Append("<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
        void Element(string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                xml.Append(CultureInfo.InvariantCulture, $"<{name}>{Esc(value)}</{name}>");
            }
        }

        Element("dc:title", Title);
        Element("dc:subject", Subject);
        Element("dc:creator", Creator);
        Element("cp:keywords", Keywords);
        Element("dc:description", Description);
        Element("dc:language", Language);
        xml.Append("<dcterms:created xsi:type=\"dcterms:W3CDTF\">2020-01-01T00:00:00Z</dcterms:created>");
        xml.Append("<dcterms:modified xsi:type=\"dcterms:W3CDTF\">2020-01-01T00:00:00Z</dcterms:modified>");
        xml.Append("</cp:coreProperties>");
        return xml.ToString();
    }

    // Motyw „Office” (2013+): dk1 000000, lt1 FFFFFF, dk2 44546A, lt2 E7E6E6, accent1–6, hlink, folHlink.
    private string ThemeXml() =>
        $"{XmlHeader}<a:theme xmlns:a=\"{DrawingNs}\" name=\"Motyw pakietu Office\"><a:themeElements>"
        + "<a:clrScheme name=\"Office\"><a:dk1><a:sysClr val=\"windowText\" lastClr=\"000000\"/></a:dk1><a:lt1><a:sysClr val=\"window\" lastClr=\"FFFFFF\"/></a:lt1>"
        + "<a:dk2><a:srgbClr val=\"44546A\"/></a:dk2><a:lt2><a:srgbClr val=\"E7E6E6\"/></a:lt2><a:accent1><a:srgbClr val=\"4472C4\"/></a:accent1>"
        + "<a:accent2><a:srgbClr val=\"ED7D31\"/></a:accent2><a:accent3><a:srgbClr val=\"A5A5A5\"/></a:accent3><a:accent4><a:srgbClr val=\"FFC000\"/></a:accent4>"
        + "<a:accent5><a:srgbClr val=\"5B9BD5\"/></a:accent5><a:accent6><a:srgbClr val=\"70AD47\"/></a:accent6><a:hlink><a:srgbClr val=\"0563C1\"/></a:hlink>"
        + "<a:folHlink><a:srgbClr val=\"954F72\"/></a:folHlink></a:clrScheme>"
        + "<a:fontScheme name=\"Office\"><a:majorFont><a:latin typeface=\"Calibri Light\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:majorFont>"
        + "<a:minorFont><a:latin typeface=\"Calibri\"/><a:ea typeface=\"\"/><a:cs typeface=\"\"/></a:minorFont></a:fontScheme>"
        + "<a:fmtScheme name=\"Office\"><a:fillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill>"
        + "<a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:fillStyleLst><a:lnStyleLst>"
        + "<a:ln w=\"6350\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln>"
        + "<a:ln w=\"19050\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln></a:lnStyleLst>"
        + "<a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>"
        + "<a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill>"
        + "<a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>";

    internal sealed class PartCounters
    {
        public int Drawing;
        public int Chart;
        public int Table;
        public int Comments;
        public int Vml;
        public int CtrlProp;
        public int ActiveX;
        public int Threaded;
    }

    /// <summary>Relacje jednej części z jawnymi, kolejnymi identyfikatorami.</summary>
    internal sealed class Rels(FixtureXlsx owner)
    {
        private readonly List<(string Id, string Type, string Target, bool External)> _items = [];

        public bool Any => _items.Count > 0;

        public string Add(string typeName, string target, bool external = false) => AddAbsoluteType(owner.RelType(typeName), target, external);

        public string AddAbsoluteType(string type, string target, bool external = false)
        {
            var id = "rId" + (_items.Count + 1).ToString(CultureInfo.InvariantCulture);
            _items.Add((id, type, target, external));
            return id;
        }

        public string ToXml()
        {
            var xml = new StringBuilder();
            xml.Append(XmlHeader).Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            foreach (var (id, type, target, external) in _items)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<Relationship Id=\"{id}\" Type=\"{type}\" Target=\"{Esc(target)}\"");
                if (external)
                {
                    xml.Append(" TargetMode=\"External\"");
                }

                xml.Append("/>");
            }

            xml.Append("</Relationships>");
            return xml.ToString();
        }
    }
}

/// <summary>Rejestr stylów fixtury: czcionki, wypełnienia, formaty komórek (<c>cellXfs</c>) i style różnicowe (<c>dxfs</c>).</summary>
public sealed class FxStyles
{
    private readonly List<string> _fonts = ["<font><sz val=\"11\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/><scheme val=\"minor\"/></font>"];
    private readonly List<string> _fills = ["<fill><patternFill patternType=\"none\"/></fill>", "<fill><patternFill patternType=\"gray125\"/></fill>"];
    private readonly List<string> _xfs = ["<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"];
    private readonly List<string> _dxfs = [];
    private readonly List<(int Id, string Code)> _numFmts = [];

    /// <summary>Własna paleta <c>indexedColors</c> (ARGB); pusta = paleta domyślna.</summary>
    public List<string> IndexedColors { get; } = [];

    /// <summary>Atrybuty koloru: <c>rgb="FF999999"</c>, <c>theme="4" tint="0.4"</c>, <c>indexed="22"</c>, <c>auto="1"</c>.</summary>
    public int Font(string? color = null, double size = 11, bool bold = false, bool italic = false, bool underline = false, string name = "Calibri")
    {
        var xml = "<font>" + (bold ? "<b/>" : "") + (italic ? "<i/>" : "") + (underline ? "<u/>" : "")
            + $"<sz val=\"{size.ToString(CultureInfo.InvariantCulture)}\"/>" + (color is null ? "" : $"<color {color}/>")
            + $"<name val=\"{name}\"/><family val=\"2\"/></font>";
        return Intern(_fonts, xml);
    }

    /// <summary>Wypełnienie: <paramref name="foreground"/> i <paramref name="background"/> to atrybuty koloru jak w <see cref="Font"/>.</summary>
    public int Fill(string? foreground, string pattern = "solid", string? background = null)
    {
        var xml = $"<fill><patternFill patternType=\"{pattern}\">" + (foreground is null ? "" : $"<fgColor {foreground}/>")
            + (background is null ? "" : $"<bgColor {background}/>") + "</patternFill></fill>";
        return Intern(_fills, xml);
    }

    /// <summary>Format komórki; zwraca indeks do atrybutu <c>s</c>.</summary>
    public int Xf(int font = 0, int fill = 0, int numFmtId = 0, string? numFmtCode = null, bool? locked = null, bool wrap = false,
        string? horizontal = null, string? rawAttributes = null)
    {
        if (numFmtCode is not null)
        {
            var existing = _numFmts.FindIndex(f => f.Code == numFmtCode);
            if (existing < 0)
            {
                _numFmts.Add((164 + _numFmts.Count, numFmtCode));
                existing = _numFmts.Count - 1;
            }

            numFmtId = _numFmts[existing].Id;
        }

        var xml = rawAttributes is not null
            ? $"<xf {rawAttributes}/>"
            : $"<xf numFmtId=\"{numFmtId}\" fontId=\"{font}\" fillId=\"{fill}\" borderId=\"0\" xfId=\"0\""
              + (numFmtId != 0 ? " applyNumberFormat=\"1\"" : "") + (font != 0 ? " applyFont=\"1\"" : "") + (fill != 0 ? " applyFill=\"1\"" : "");
        if (rawAttributes is null)
        {
            var alignment = wrap || horizontal is not null
                ? "<alignment" + (horizontal is null ? "" : $" horizontal=\"{horizontal}\"") + (wrap ? " wrapText=\"1\"" : "") + "/>"
                : "";
            var protection = locked is null ? "" : $"<protection locked=\"{(locked.Value ? 1 : 0)}\"/>";
            xml += (alignment.Length > 0 ? " applyAlignment=\"1\"" : "") + (protection.Length > 0 ? " applyProtection=\"1\"" : "");
            xml += alignment.Length + protection.Length > 0 ? ">" + alignment + protection + "</xf>" : "/>";
        }

        return Intern(_xfs, xml);
    }

    /// <summary>Styl różnicowy z surowego wnętrza elementu <c>dxf</c>; zwraca <c>dxfId</c>.</summary>
    public int Dxf(string innerXml)
    {
        _dxfs.Add($"<dxf>{innerXml}</dxf>");
        return _dxfs.Count - 1;
    }

    /// <summary>Dodatkowy styl nazwany (<c>cellStyleXfs</c>) – surowe elementy <c>xf</c> za stylem „Normalny”.</summary>
    public List<string> ExtraStyleXfs { get; } = [];

    internal string ToXml(string mainNs)
    {
        var xml = new StringBuilder();
        xml.Append(FixtureXlsx.XmlHeader).Append(CultureInfo.InvariantCulture, $"<styleSheet xmlns=\"{mainNs}\">");
        if (_numFmts.Count > 0)
        {
            xml.Append(CultureInfo.InvariantCulture, $"<numFmts count=\"{_numFmts.Count}\">");
            foreach (var (id, code) in _numFmts)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<numFmt numFmtId=\"{id}\" formatCode=\"{FixtureXlsx.Esc(code)}\"/>");
            }

            xml.Append("</numFmts>");
        }

        Section(xml, "fonts", _fonts);
        Section(xml, "fills", _fills);
        xml.Append("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
        Section(xml, "cellStyleXfs", ["<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>", .. ExtraStyleXfs]);
        Section(xml, "cellXfs", _xfs);
        xml.Append("<cellStyles count=\"1\"><cellStyle name=\"Normalny\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
        if (_dxfs.Count > 0)
        {
            Section(xml, "dxfs", _dxfs);
        }
        else
        {
            xml.Append("<dxfs count=\"0\"/>");
        }

        xml.Append("<tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium2\" defaultPivotStyle=\"PivotStyleLight16\"/>");
        if (IndexedColors.Count > 0)
        {
            xml.Append("<colors><indexedColors>");
            foreach (var color in IndexedColors)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<rgbColor rgb=\"{color}\"/>");
            }

            xml.Append("</indexedColors></colors>");
        }

        xml.Append("</styleSheet>");
        return xml.ToString();
    }

    private static void Section(StringBuilder xml, string name, IReadOnlyList<string> items)
    {
        xml.Append(CultureInfo.InvariantCulture, $"<{name} count=\"{items.Count}\">");
        foreach (var item in items)
        {
            xml.Append(item);
        }

        xml.Append(CultureInfo.InvariantCulture, $"</{name}>");
    }

    private static int Intern(List<string> list, string xml)
    {
        var index = list.IndexOf(xml);
        if (index >= 0)
        {
            return index;
        }

        list.Add(xml);
        return list.Count - 1;
    }
}
