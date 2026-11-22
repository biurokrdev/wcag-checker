using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace D2.Wcag.SampleGenerator;

/// <summary>
/// Klocki pism przykładowych: parametry standardu pism firmowych 2.0 wzięte z kodu reguł (Corporate20Rules.cs,
/// RegistrationData20.cs, Footer20Rules.cs, TableCells20.cs) i z wzorcowego szablonu samples/TemplateGenerator.
/// </summary>
internal static class LetterParts
{
    // Paleta (Spec20): czarny, pomarańcz (tylko H1 i punktory), niebieski, fuksja, fiolet (łącza), szary (stopka, dane rejestrowe).
    public const string Black = "000000";
    public const string Orange = "FF6200";
    public const string Violet = "7724FF";
    public const string Gray = "696969";

    public const string TextFont = Standards.CorporateStandard20.TextFont;     // „SUN Me”
    public const string DisplayFont = Standards.CorporateStandard20.DisplayFont; // „SUN Display”

    // Wymiary w twipach (1 pt = 20 twipów).
    public const int A4Width = 11906;
    public const int A4Height = 16838;
    public const int Margin = 720;          // 36 pt = 12,7 mm (MarginRule 36 pt, standard 1.9 i 2.0)
    public const string Line12 = "288";     // interlinia 1,2 (LineSpacing20Rule)
    public const string Line15 = "360";     // interlinia 1,5 (dane rejestrowe)
    public const string H1Before = "850";   // 42,5 pt (HeadingSpacing20Rule)
    public const string H1After = "680";    // 34 pt

    public static RunFonts Fonts(string name) => new() { Ascii = name, HighAnsi = name, ComplexScript = name, EastAsia = name };

    public static Run Text(string text, RunProperties? properties = null)
    {
        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        if (properties is not null)
        {
            run.PrependChild(properties);
        }

        return run;
    }

    public static Paragraph Para(string text) => new(Text(text));

    public static Paragraph Para(params OpenXmlElement[] content) => new(content);

    public static Paragraph Styled(string styleId, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = styleId }), Text(text));

    public static Paragraph Styled(string styleId, params OpenXmlElement[] content) =>
        new([new ParagraphProperties(new ParagraphStyleId { Val = styleId }), .. content]);

    public static Paragraph Heading(int level, string text) => Styled($"Nagwek{level}", text);

    public static Paragraph ListItem(int numId, string text, int level = 0) => new(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "Akapitzlist" },
            new NumberingProperties(new NumberingLevelReference { Val = level }, new NumberingId { Val = numId })),
        Text(text));

    public static Run CharStyled(string styleId, string text) =>
        Text(text, new RunProperties(new RunStyle { Val = styleId }));

    public static Hyperlink Link(OpenXmlPartContainer part, string text, string url, Run? run = null)
    {
        var relationship = part switch
        {
            MainDocumentPart main => main.AddHyperlinkRelationship(new Uri(url), true, $"rIdLink{main.HyperlinkRelationships.Count() + 1}"),
            _ => throw new ArgumentException("Łącza tylko w treści głównej.", nameof(part)),
        };
        return new Hyperlink(run ?? CharStyled("Hipercze", text)) { Id = relationship.Id, History = true };
    }

    // ------------------------------------------------------------ style

    public static Style HeadingStyle(int level, string font, string size, string color, string before, string after) => new(
        new StyleName { Val = $"heading {level}" },
        new BasedOn { Val = "Normalny" },
        new NextParagraphStyle { Val = "Normalny" },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new KeepLines(),
            new SpacingBetweenLines { Before = before, After = after, Line = Line12, LineRule = LineSpacingRuleValues.Auto },
            new OutlineLevel { Val = level - 1 }),
        new StyleRunProperties(Fonts(font), new Bold(), new BoldComplexScript(), new Color { Val = color }, new FontSize { Val = size }, new FontSizeComplexScript { Val = size }))
    {
        Type = StyleValues.Paragraph,
        StyleId = $"Nagwek{level}",
    };

    public static Style ParagraphStyle(string id, string name, StyleRunProperties? run, StyleParagraphProperties? paragraph = null,
        bool gallery = false, bool custom = false, string basedOn = "Normalny")
    {
        var style = new Style(new StyleName { Val = name }, new BasedOn { Val = basedOn }) { Type = StyleValues.Paragraph, StyleId = id };
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

        if (custom)
        {
            style.CustomStyle = true;
        }

        return style;
    }

    public static Style CharacterStyle(string id, string name, StyleRunProperties run) =>
        new(new StyleName { Val = name }, new PrimaryStyle(), run) { Type = StyleValues.Character, StyleId = id };

    /// <summary>Styl tabeli: krawędzie pojedyncze 1 pt (sz=8) czarne, komórki wyśrodkowane w pionie, tekst do lewej.</summary>
    public static Style TableStyle(int borderSize = 8, string borderColor = Black, bool center = true)
    {
        BorderType Border<T>() where T : BorderType, new() => new T { Val = BorderValues.Single, Size = (uint)borderSize, Space = 0U, Color = borderColor };

        var style = new Style(
            new StyleName { Val = "Tabela standardowa" },
            new PrimaryStyle(),
            new StyleParagraphProperties(new SpacingBetweenLines { After = "0", Line = Line12, LineRule = LineSpacingRuleValues.Auto }, new Justification { Val = JustificationValues.Left }),
            new StyleTableProperties(
                new TableBorders(
                    (TopBorder)Border<TopBorder>(), (LeftBorder)Border<LeftBorder>(), (BottomBorder)Border<BottomBorder>(),
                    (RightBorder)Border<RightBorder>(), (InsideHorizontalBorder)Border<InsideHorizontalBorder>(), (InsideVerticalBorder)Border<InsideVerticalBorder>()),
                new TableCellMarginDefault(
                    new TopMargin { Width = "57", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 85, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "57", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 85, Type = TableWidthValues.Dxa })))
        {
            Type = StyleValues.Table,
            StyleId = "Tabelastandardowa",
        };
        if (center)
        {
            style.Append(new StyleTableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
        }

        return style;
    }

    public static DocDefaults DocDefaults(string font = TextFont) => new(
        new RunPropertiesDefault(new RunPropertiesBaseStyle(
            Fonts(font),
            new Color { Val = Black },
            new FontSize { Val = "20" },
            new FontSizeComplexScript { Val = "20" },
            new Languages { Val = "pl-PL", EastAsia = "pl-PL", Bidi = "ar-SA" })),
        new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
            new SpacingBetweenLines { After = "120", Line = Line12, LineRule = LineSpacingRuleValues.Auto },
            new Justification { Val = JustificationValues.Left })));

    public static void AddSettings(MainDocumentPart main, bool trackRevisions = false)
    {
        var part = main.AddNewPart<DocumentSettingsPart>("rIdSettings");
        part.Settings = new Settings(
            new DisplayBackgroundShape(),
            new DefaultTabStop { Val = 708 },
            new CharacterSpacingControl { Val = CharacterSpacingValues.DoNotCompress },
            new ThemeFontLanguages { Val = "pl-PL" });
        if (trackRevisions)
        {
            part.Settings.InsertAt(new TrackRevisions(), 1);
        }
    }

    // ------------------------------------------------------------ grafika

    public static Drawing InlinePicture(OpenXmlPart owner, byte[] png, uint id, string name, string? description, long widthMm10, long heightMm10)
    {
        var (embed, cx, cy) = PictureData(owner, png, widthMm10, heightMm10);
        var properties = new DW.DocProperties { Id = id, Name = name };
        if (description is not null)
        {
            properties.Description = description;
        }

        return new Drawing(new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            properties,
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            Graphic(embed, id, name, cx, cy))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
        });
    }

    /// <summary>Obraz pływający (kotwica z opływaniem kwadratowym) przy prawym marginesie.</summary>
    public static Drawing AnchoredPicture(OpenXmlPart owner, byte[] png, uint id, string name, string description, long widthMm10, long heightMm10)
    {
        var (embed, cx, cy) = PictureData(owner, png, widthMm10, heightMm10);
        return new Drawing(new DW.Anchor(
            new DW.SimplePosition { X = 0L, Y = 0L },
            new DW.HorizontalPosition(new DW.HorizontalAlignment("right")) { RelativeFrom = DW.HorizontalRelativePositionValues.Margin },
            new DW.VerticalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.VerticalRelativePositionValues.Paragraph },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.WrapSquare { WrapText = DW.WrapTextValues.BothSides },
            new DW.DocProperties { Id = id, Name = name, Description = description },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            Graphic(embed, id, name, cx, cy))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 114300U, DistanceFromRight = 0U,
            SimplePos = false, RelativeHeight = 251659264U, BehindDoc = false, Locked = false, LayoutInCell = true, AllowOverlap = true,
        });
    }

    private static (string Embed, long Cx, long Cy) PictureData(OpenXmlPart owner, byte[] png, long widthMm10, long heightMm10)
    {
        ImagePart image = owner switch
        {
            MainDocumentPart main => main.AddImagePart(ImagePartType.Png, $"rIdImage{main.Parts.Count() + 1}"),
            HeaderPart header => header.AddImagePart(ImagePartType.Png, $"rIdImage{header.Parts.Count() + 1}"),
            _ => throw new ArgumentException("Nieobsługiwana część.", nameof(owner)),
        };
        image.FeedData(new MemoryStream(png));
        // Wymiary w dziesiątych częściach milimetra → EMU (1 mm = 36 000 EMU).
        return (owner.GetIdOfPart(image), widthMm10 * 3600L, heightMm10 * 3600L);
    }

    private static A.Graphic Graphic(string embed, uint id, string name, long cx, long cy) => new(new A.GraphicData(new PIC.Picture(
        new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = id, Name = name }, new PIC.NonVisualPictureDrawingProperties()),
        new PIC.BlipFill(new A.Blip { Embed = embed }, new A.Stretch(new A.FillRectangle())),
        new PIC.ShapeProperties(
            new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
    {
        Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
    });

    // ------------------------------------------------------------ pakiet

    /// <summary>Zapis pakietu: metadane ze stałymi datami, właściwości aplikacji, potem deterministyczne przepakowanie.</summary>
    public static byte[] Finish(MemoryStream stream) => DeterministicZip.Repack(stream.ToArray());

    public static void SetProperties(WordprocessingDocument package, string? title, string? creator, string? language, string pages, string? subject = null)
    {
        var created = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        package.PackageProperties.Title = title;
        package.PackageProperties.Creator = creator;
        package.PackageProperties.Language = language;
        package.PackageProperties.Subject = subject;
        package.PackageProperties.Created = created;
        package.PackageProperties.Modified = created;
        package.PackageProperties.LastModifiedBy = creator;

        var app = package.AddNewPart<ExtendedFilePropertiesPart>("rIdApp");
        app.Properties = new DocumentFormat.OpenXml.ExtendedProperties.Properties(
            new DocumentFormat.OpenXml.ExtendedProperties.Pages(pages),
            new DocumentFormat.OpenXml.ExtendedProperties.Application("D2.Wcag SampleGenerator"));
    }
}
