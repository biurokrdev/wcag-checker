using System.Globalization;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

/// <summary>
/// Efektywne właściwości komórki i wiersza tabeli (rozdz. 9): wartość z komórki (w:tcPr, w:tcBorders) albo wiersza,
/// a gdy jej nie ma – z tabeli i ze stylu tabeli, razem z formatowaniem warunkowym w:tblStylePr (pierwszy wiersz,
/// pasy, narożniki). Warstwy stylu podaje <see cref="DocxDocument.TableStyleProperties"/>; tutaj są składane
/// z właściwościami bezpośrednimi.
/// </summary>
internal static class TableCells20
{
    private static readonly string[] Sides = ["top", "left", "bottom", "right"];

    private static readonly Dictionary<string, string> SideNames = new(StringComparer.Ordinal)
    {
        ["top"] = "górna",
        ["left"] = "lewa",
        ["bottom"] = "dolna",
        ["right"] = "prawa",
    };

    /// <summary>Wyrównanie pionowe komórki: w:vAlign komórki, potem stylu tabeli; domyślnie do góry.</summary>
    public static string VerticalAlignment(TableCell cell, DocxDocument document)
    {
        if (Value(Child(cell.TableCellProperties, "vAlign")) is { } direct)
        {
            return direct;
        }

        return document.TableStyleProperties(cell, "tcPr").Select(layer => Value(Child(layer, "vAlign"))).LastOrDefault(v => v is not null) ?? "top";
    }

    /// <summary>„Nie dziel wiersza między stronami”: w:cantSplit wiersza albo stylu tabeli.</summary>
    public static bool CantSplit(TableRow row, DocxDocument document) => RowFlag(row, document, "cantSplit");

    /// <summary>
    /// Krawędzie komórki, które nie są pojedynczą linią 1 pt (w:sz = 8) w kolorze czarnym albo automatycznym;
    /// opis każdej po polsku, np. „górna 0,5 pt #FF0000”. Pusta lista = wszystkie cztery krawędzie zgodne.
    /// </summary>
    public static IReadOnlyList<string> BorderProblems(TableCell cell, DocxDocument document)
    {
        var edges = Borders(cell, document);
        var problems = new List<string>();
        foreach (var side in Sides)
        {
            var edge = edges[side];
            var style = Value(edge);
            var size = Attribute(edge, "sz");
            var color = Attribute(edge, "color");
            var colorOk = color is null || color.Equals("auto", StringComparison.OrdinalIgnoreCase) || color.Equals(Spec20.Black, StringComparison.OrdinalIgnoreCase);
            if (edge is not null && style == "single" && size == "8" && colorOk)
            {
                continue;
            }

            problems.Add(edge is null || style is null or "nil" or "none"
                ? $"{SideNames[side]}: brak"
                : $"{SideNames[side]} {Describe(style, size, color)}");
        }

        return problems;
    }

    private static string Describe(string style, string? size, string? color)
    {
        var parts = new List<string>();
        if (style != "single")
        {
            parts.Add($"linia „{style}”");
        }

        if (double.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out var eighths))
        {
            parts.Add($"{(eighths / 8).ToString("0.##", CultureInfo.GetCultureInfo("pl-PL"))} pt");
        }

        if (color is not null && !color.Equals("auto", StringComparison.OrdinalIgnoreCase) && !color.Equals(Spec20.Black, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("#" + color.ToUpperInvariant());
        }

        return string.Join(" ", parts);
    }

    // Krawędzie efektywne: krawędzie tabeli ze stylu (także warunkowe), krawędzie komórek ze stylu, potem krawędzie
    // tabeli nadane wprost, wyjątki wiersza (w:tblPrEx) i na końcu w:tcBorders komórki. Każda krawędź osobno:
    // późniejsza warstwa nadpisuje tylko te krawędzie, które sama ustawia.
    private static Dictionary<string, OpenXmlElement?> Borders(TableCell cell, DocxDocument document)
    {
        var edges = Sides.ToDictionary(s => s, _ => (OpenXmlElement?)null, StringComparer.Ordinal);
        var row = cell.Ancestors<TableRow>().FirstOrDefault();
        var table = cell.Ancestors<Table>().FirstOrDefault();
        if (row is null || table is null)
        {
            return edges;
        }

        var rows = table.Elements<TableRow>().ToList();
        var cells = row.Elements<TableCell>().ToList();
        var position = new Position(
            rows.FindIndex(r => ReferenceEquals(r, row)) == 0,
            rows.FindIndex(r => ReferenceEquals(r, row)) == rows.Count - 1,
            cells.FindIndex(c => ReferenceEquals(c, cell)) == 0,
            cells.FindIndex(c => ReferenceEquals(c, cell)) == cells.Count - 1);

        void Apply(OpenXmlElement? borders, Position at)
        {
            if (borders is null)
            {
                return;
            }

            Set("top", at.Top ? Child(borders, "top") : Child(borders, "insideH"));
            Set("bottom", at.Bottom ? Child(borders, "bottom") : Child(borders, "insideH"));
            Set("left", at.Left ? Child(borders, "left") ?? Child(borders, "start") : Child(borders, "insideV"));
            Set("right", at.Right ? Child(borders, "right") ?? Child(borders, "end") : Child(borders, "insideV"));
        }

        void Set(string side, OpenXmlElement? border)
        {
            if (border is not null)
            {
                edges[side] = border;
            }
        }

        foreach (var layer in document.TableStyleProperties(cell, "tblPr"))
        {
            Apply(Child(layer, "tblBorders"), Region(layer, position));
        }

        foreach (var layer in document.TableStyleProperties(cell, "tcPr"))
        {
            Apply(Child(layer, "tcBorders"), Region(layer, position));
        }

        Apply(Child(table.GetFirstChild<TableProperties>(), "tblBorders"), position);
        Apply(Child(Child(row, "tblPrEx"), "tblBorders"), position with { Top = true, Bottom = true });
        Apply(Child(cell.TableCellProperties, "tcBorders"), new Position(true, true, true, true));
        return edges;
    }

    // Położenie komórki w obszarze, którego dotyczy warstwa stylu: cała tabela, wiersz (pierwszy, ostatni, pas poziomy),
    // kolumna (pierwsza, ostatnia, pas pionowy) albo pojedyncza komórka narożna. Od niego zależy, czy krawędź komórki
    // bierze się z krawędzi zewnętrznej obszaru, czy z wewnętrznej (insideH, insideV).
    private static Position Region(OpenXmlElement layer, Position inTable)
    {
        var type = layer.Parent is TableStyleProperties conditional ? conditional.Type?.InnerText : null;
        return type switch
        {
            "firstRow" or "lastRow" or "band1Horz" or "band2Horz" => inTable with { Top = true, Bottom = true },
            "firstCol" or "lastCol" or "band1Vert" or "band2Vert" => inTable with { Left = true, Right = true },
            "nwCell" or "neCell" or "swCell" or "seCell" => new Position(true, true, true, true),
            _ => inTable,
        };
    }

    private static bool RowFlag(TableRow row, DocxDocument document, string localName)
    {
        if (Child(row.TableRowProperties, localName) is { } direct)
        {
            return IsOn(direct);
        }

        var cell = row.Elements<TableCell>().FirstOrDefault();
        var styled = cell is null ? null : document.TableStyleProperties(cell, "trPr").Select(layer => Child(layer, localName)).LastOrDefault(e => e is not null);
        return styled is not null && IsOn(styled);
    }

    private static bool IsOn(OpenXmlElement flag) => Value(flag) is null or "1" or "true" or "on";

    private static OpenXmlElement? Child(OpenXmlElement? parent, string localName) =>
        parent?.ChildElements.FirstOrDefault(e => e.LocalName == localName);

    private static string? Value(OpenXmlElement? element) => Attribute(element, "val");

    private static string? Attribute(OpenXmlElement? element, string localName)
    {
        if (element is null)
        {
            return null;
        }

        foreach (var attribute in element.GetAttributes())
        {
            if (attribute.LocalName == localName)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private readonly record struct Position(bool Top, bool Bottom, bool Left, bool Right);
}
