using D2.Wcag.Standards;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests;

// Dokument zgodny ze standardem pism firmowych 2.0, budowany w pamięci dla testów reguł tego standardu: każdy test
// psuje tylko to, co sprawdza.
internal sealed partial class DocxBuilder
{
    public const string Bank20 = CorporateStandard20.DefaultInstitution;

    /// <summary>Metadane zgodne z rozdz. 17: tytuł „[Tytuł]. Bank”, autor = bank.</summary>
    public static DocxBuilder Compliant20() => new() { Title = $"Regulamin promocji. {Bank20}", Author = Bank20 };

    /// <summary>
    /// Buduje dokument i dokłada to, czego wymaga standard 2.0: docDefaults SUN Me 10 pt, interlinia 1,2, nagłówki według
    /// tabeli 3.1, H1 z odstępami 42,5/34 pt, stopka „Strona: X z Y. Regulamin promocji” 8 pt #696969.
    /// <paramref name="tweak"/> psuje wybrany element już po tych poprawkach.
    /// </summary>
    public byte[] Build20(Action<MainDocumentPart>? tweak = null)
    {
        var bytes = Build();
        using var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var main = package.MainDocumentPart!;
            var styles = main.StyleDefinitionsPart!.Styles!;
            styles.DocDefaults = new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "SUN Me", HighAnsi = "SUN Me" }, new Color { Val = "000000" }, new FontSize { Val = "20" }, new Languages { Val = "pl-PL" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(new SpacingBetweenLines { Line = "288", LineRule = LineSpacingRuleValues.Auto })));

            void HeadingRun(int level, string font, string size, string color, SpacingBetweenLines? spacing = null)
            {
                var style = styles.Elements<Style>().Single(s => s.StyleId == $"Nagwek{level}");
                style.Append(new StyleRunProperties(new RunFonts { Ascii = font, HighAnsi = font }, new Bold(), new Color { Val = color }, new FontSize { Val = size }));
                if (spacing is not null)
                {
                    style.StyleParagraphProperties!.Append(spacing);
                }
            }

            HeadingRun(1, "SUN Display", "48", "FF6200", new SpacingBetweenLines { Before = "850", After = "680", Line = "288", LineRule = LineSpacingRuleValues.Auto });
            HeadingRun(2, "SUN Me", "32", "000000");
            HeadingRun(3, "SUN Me", "28", "0000FF");
            HeadingRun(4, "SUN Me", "24", "000000");

            if (main.FooterParts.SingleOrDefault() is { } footer)
            {
                footer.Footer = new Footer(Footer20Paragraph("Regulamin promocji"));
            }

            tweak?.Invoke(main);
            package.Save();
        }

        return stream.ToArray();
    }

    /// <summary>Fragment stopki w stylu z rozdz. 11: 8 pt, #696969 (czcionka i interlinia z ustawień domyślnych).</summary>
    public static Run Footer20Run(string text) => new(
        new RunProperties(new Color { Val = "696969" }, new FontSize { Val = "16" }),
        new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    /// <summary>Akapit stopki w dosłownej postaci „Strona: X z Y. Tytuł”.</summary>
    public static Paragraph Footer20Paragraph(string title) => new(
        Footer20Run("Strona: "),
        new SimpleField(Footer20Run("1")) { Instruction = " PAGE " },
        Footer20Run(" z "),
        new SimpleField(Footer20Run("1")) { Instruction = " NUMPAGES " },
        Footer20Run($". {title}"));

    /// <summary>
    /// Tabela danych zgodna z rozdz. 9: krawędzie 1 pt czarne we właściwościach tabeli, wiersz nagłówkowy pogrubiony,
    /// komórki wyśrodkowane w pionie, wiersze niepodzielne, tytuł i opis w tekście alternatywnym.
    /// </summary>
    public static Table DataTable20(Action<Table>? tweak = null)
    {
        static TableCell Cell(string text, bool bold) => new(
            new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
            new Paragraph(new Run(bold ? new RunProperties(new Bold()) : new RunProperties(), new Text(text))));

        var table = new Table(
            new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 8, Color = "000000" }, new BottomBorder { Val = BorderValues.Single, Size = 8, Color = "000000" },
                    new LeftBorder { Val = BorderValues.Single, Size = 8, Color = "000000" }, new RightBorder { Val = BorderValues.Single, Size = 8, Color = "000000" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 8, Color = "000000" }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 8, Color = "000000" }),
                new TableCaption { Val = "Opłaty" },
                new TableDescription { Val = "Nazwa opłaty i jej kwota." }),
            new TableRow(new TableRowProperties(new CantSplit(), new TableHeader()), Cell("Nazwa", true), Cell("Kwota", true)),
            new TableRow(new TableRowProperties(new CantSplit()), Cell("Opłata", false), Cell("10 zł", false)));
        tweak?.Invoke(table);
        return table;
    }
}
