using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests;

// Standard pism firmowych 2.0 według specyfikacji guidebooka: dokument w pełni zgodny przechodzi bez zgłoszeń,
// a każde naruszenie z rozdz. 20 (G-01…G-16) daje swój kod z wystąpieniem.
public class CorporateStandard20Tests
{
    private const string Bank = CorporateStandard20.DefaultInstitution;

    private static readonly WcagValidationProfile Only20 = new WcagValidationProfile().WithStandards("firmowy/2.0");

    private static WcagValidationResult Validate(byte[] document) => new WcagDocxValidator().Validate(document, Only20);

    // Szablon zgodny ze standardem: docDefaults OHH Me 10 pt, interlinia 1,2, nagłówki według tabeli 3.1, H1 z odstępami
    // 42,5/34 pt, stopka „Strona: N. Tytuł” 8 pt #696969, metadane „[Tytuł]. Bank”, autor = bank.
    private static DocxBuilder Compliant() => new() { Title = $"Regulamin promocji. {Bank}", Author = Bank };

    private static byte[] Build(DocxBuilder builder, Action<MainDocumentPart>? tweak = null)
    {
        var bytes = builder.Build();
        using var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var main = package.MainDocumentPart!;
            var styles = main.StyleDefinitionsPart!.Styles!;
            styles.DocDefaults = new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "OHH Me", HighAnsi = "OHH Me" }, new Color { Val = "000000" }, new FontSize { Val = "20" }, new Languages { Val = "pl-PL" })),
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

            HeadingRun(1, "OHH Display", "48", "FF6200", new SpacingBetweenLines { Before = "850", After = "680", Line = "288", LineRule = LineSpacingRuleValues.Auto });
            HeadingRun(2, "OHH Me", "32", "000000");
            HeadingRun(3, "OHH Me", "28", "0000FF");
            HeadingRun(4, "OHH Me", "24", "000000");

            var footer = main.FooterParts.Single();
            footer.Footer = new Footer(new Paragraph(
                new Run(new RunProperties(new Color { Val = "696969" }, new FontSize { Val = "16" }), new Text("Strona: ") { Space = SpaceProcessingModeValues.Preserve }),
                new SimpleField(new Run(new RunProperties(new Color { Val = "696969" }, new FontSize { Val = "16" }), new Text("1"))) { Instruction = " PAGE " },
                new Run(new RunProperties(new Color { Val = "696969" }, new FontSize { Val = "16" }), new Text(". Regulamin promocji") { Space = SpaceProcessingModeValues.Preserve })));

            tweak?.Invoke(main);
            package.Save();
        }

        return stream.ToArray();
    }

    private static Table DataTable(Action<Table>? tweak = null)
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

    [Fact]
    public void Standard20_IsInTheCatalog_WithChecksAndNote()
    {
        var standard = StandardCatalog.Default.Get("firmowy/2.0");

        Assert.Equal("Standard pism firmowych 2.0", standard.Name);
        Assert.True(standard.Confirmed);
        Assert.Contains(standard.Checks, c => c.Name.StartsWith("G-01"));
        Assert.Contains(standard.Checks, c => c.Manual && c.Name.StartsWith("G-15"));
        var codes = standard.Checks.SelectMany(c => c.Codes).ToHashSet();
        Assert.All(new[] { IssueCodes.JustificationForbidden, IssueCodes.HeadingStyleMismatch, IssueCodes.TableEmptyCell, IssueCodes.VariantMarkerUnbalanced }, c => Assert.Contains(c, codes));
        Assert.All(codes, c => Assert.NotEqual(c.ToLowerInvariant().Replace('_', ' '), IssueLabels.Of(c)));
    }

    [Fact]
    public void CompliantDocument_HasNoCorporate20Issues()
    {
        var document = Build(Compliant()
            .Heading(1, "Regulamin")
            .Paragraph("Treść regulaminu.")
            .Heading(2, "Zakres")
            .Paragraph("Tabela opłat")
            .Add(DataTable()));

        var result = Validate(document);

        Assert.True(result.Issues.Count == 0, string.Join("\n", result.Issues.Select(i => $"{i.Code}: {i.Message}")));
    }

    [Fact]
    public void PageAndMargins_AreChecked_G01()
    {
        var result = Validate(Build(Compliant().Heading(1, "A"), main =>
            main.Document.Body!.Descendants<PageMargin>().Single().Top = 1134));

        Assert.Contains(result.Issues, i => i.Code == IssueCodes.InvalidMargins && i.Message.Contains("górny"));
    }

    [Fact]
    public void JustifiedParagraph_IsAnError_AndFixAlignsLeft_G04()
    {
        var document = Build(Compliant().Heading(1, "A").Add(new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Both }), new Run(new Text("Wyjustowany akapit.")))));

        var result = Validate(document);
        var fixedResult = new WcagDocxFixer().Fix(document, Only20);

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.JustificationForbidden);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Single(issue.Locations);
        Assert.DoesNotContain(fixedResult.After.Issues, i => i.Code == IssueCodes.JustificationForbidden);
    }

    [Fact]
    public void FontFamily_MustBeIngMe_AndWeightNotInTheName()
    {
        var result = Validate(Build(Compliant().Heading(1, "A")
            .Runs(new Run(new RunProperties(new RunFonts { Ascii = "Calibri" }), new Text("Calibri")))
            .Runs(new Run(new RunProperties(new RunFonts { Ascii = "OHH Me Bold" }), new Text("Zła nazwa")))));

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.FontFamilyMismatch && i.Message.Contains("Calibri"));
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.FontWeightInFamilyName);
    }

    [Fact]
    public void LineSpacingOtherThan12_IsAnError()
    {
        var result = Validate(Build(Compliant().Heading(1, "A").Add(new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Line = "360", LineRule = LineSpacingRuleValues.Auto }), new Run(new Text("Interlinia 1,5."))))));

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.LineSpacingMismatch);
        Assert.Contains("1,5", issue.Locations.Single().Snippet);
    }

    [Fact]
    public void BodyText_10ptIsFine_12ptIsAWarning_11ptIsAnError()
    {
        var result = Validate(Build(Compliant().Heading(1, "A")
            .Runs(new Run(new RunProperties(new FontSize { Val = "24" }), new Text("Powiększony")))
            .Runs(new Run(new RunProperties(new FontSize { Val = "22" }), new Text("Jedenastka")))));

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.BodyTextSizeMismatch && i.Locations.Single().Snippet.Contains("12 pt"));
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.BodyTextSizeMismatch && i.Locations.Single().Snippet.Contains("11 pt"));
    }

    [Fact]
    public void HeadingTypography_IsChecked_G03()
    {
        var result = Validate(Build(Compliant()
            .Heading(1, "Tytuł")
            .Add(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Nagwek2" }),
                new Run(new RunProperties(new Bold { Val = false }, new Color { Val = "FF6200" }), new Text("Zły H2"))))));

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.HeadingStyleMismatch);
        Assert.Contains("H2", issue.Message);
        Assert.Contains("brak pogrubienia", issue.Message);
        Assert.Contains("#FF6200", issue.Message);
    }

    [Fact]
    public void H1At32pt_MustBeOrange()
    {
        var result = Validate(Build(Compliant().Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Nagwek1" }),
            new Run(new RunProperties(new FontSize { Val = "64" }, new Color { Val = "0000FF" }), new Text("Strona tytułowa"))))));

        Assert.Contains(result.Issues, i => i.Code == IssueCodes.HeadingStyleMismatch && i.Message.Contains("#FF6200"));
    }

    [Fact]
    public void Palette_AllowsSpecColors_AndRejectsOthers_G02()
    {
        var result = Validate(Build(Compliant().Heading(1, "A")
            .Runs(new Run(new RunProperties(new Color { Val = "0000FF" }), new Text("niebieski zmienny")))
            .Runs(new Run(new RunProperties(new Color { Val = "00AA00" }), new Text("zielony")))));

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.TextColorNotAllowed);
        Assert.Single(issue.Locations);
        Assert.Contains("#00AA00", issue.Locations[0].Snippet);
    }

    [Fact]
    public void LinkMustBeVioletAndUnderlined()
    {
        var result = Validate(Build(Compliant().Heading(1, "A").Hyperlink("Regulamin promocji", "https://example.org/a")));

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.LinkStyleMismatch);
        Assert.Contains("bez podkreślenia", issue.Locations.Single().Snippet);
    }

    [Fact]
    public void H1Spacing_IsAWarningWhenOff_G05()
    {
        var result = Validate(Build(Compliant().Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Nagwek1" }, new SpacingBetweenLines { Before = "0", After = "0", Line = "288", LineRule = LineSpacingRuleValues.Auto }),
            new Run(new Text("Bez odstępów"))))));

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.HeadingSpacingMismatch && i.Message.Contains("0 pt nad"));
    }

    [Fact]
    public void Footer_MustBe8ptGray_WithTitle_G11()
    {
        var result = Validate(Build(Compliant().Heading(1, "A"), main =>
            main.FooterParts.Single().Footer = new Footer(new Paragraph(new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " }))));

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.FooterStyleMismatch);
        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.FooterTitleMissing);
    }

    [Fact]
    public void Toc_RequiredAboveNinePages_AndItsHeadingIsNotAHeading_G06()
    {
        var result = Validate(Build(Compliant()
            .Heading(1, "Dokument")
            .Heading(2, "Spis treści"), main =>
        {
            var app = ((WordprocessingDocument)main.OpenXmlPackage).AddExtendedFilePropertiesPart();
            app.Properties = new DocumentFormat.OpenXml.ExtendedProperties.Properties(new DocumentFormat.OpenXml.ExtendedProperties.Pages("12"));
        }));

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.TocRequired && i.Message.Contains("12 stron"));
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TocHeadingInToc);
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TocHeadingStyleMismatch);
    }

    [Fact]
    public void Tables_EmptyCellsSplittingRowsBordersHeadersAlignmentAndTitle_G08_G09()
    {
        var table = DataTable(t =>
        {
            t.GetFirstChild<TableProperties>()!.TableBorders!.InsideVerticalBorder!.Size = 4;
            t.GetFirstChild<TableProperties>()!.GetFirstChild<TableCaption>()!.Remove();
            var rows = t.Elements<TableRow>().ToList();
            rows[1].TableRowProperties!.GetFirstChild<CantSplit>()!.Remove();
            rows[1].Elements<TableCell>().Last().Elements<Paragraph>().Single().RemoveAllChildren<Run>();
            rows[0].Elements<TableCell>().First().Descendants<Bold>().Single().Remove();
            rows[1].Elements<TableCell>().First().TableCellProperties!.RemoveAllChildren<TableCellVerticalAlignment>();
        });

        var result = Validate(Build(Compliant().Heading(1, "A").Paragraph("Tabela").Add(table)));

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TableEmptyCell && i.Locations.Single().Kind == LocationKind.TableCell);
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TableRowCanSplit);
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TableBorderMismatch);
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TableHeaderNotBold);
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TableAlignmentMismatch);
        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.TableTitleMissing);

        var fixedResult = new WcagDocxFixer().Fix(Build(Compliant().Heading(1, "A").Paragraph("Tabela").Add((Table)table.CloneNode(true))), Only20);
        Assert.DoesNotContain(fixedResult.After.Issues, i => i.Code == IssueCodes.TableRowCanSplit);
    }

    [Fact]
    public void Metadata_TitleSuffixAndAuthor_AreErrors_AndFixed_G12()
    {
        var document = Build(new DocxBuilder { Title = "Regulamin promocji", Author = "Jan Kowalski" }.Heading(1, "A"));

        var result = Validate(document);
        var fixedResult = new WcagDocxFixer().Fix(document, Only20);

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TitleSuffixInvalid);
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.AuthorNotInstitution);
        Assert.Equal($"Regulamin promocji. {Bank}", fixedResult.After.Document!.Title);
        Assert.Equal(Bank, fixedResult.After.Document.Author);
        Assert.DoesNotContain(fixedResult.After.Issues, i => i.Category == IssueCategory.Metadata);
    }

    [Fact]
    public void Variants_MustBeNestedAndClosed_WithRightColors_G14()
    {
        Run Fuchsia(string text) => new(new RunProperties(new Color { Val = "AB0066" }), new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        Run Blue(string text) => new(new RunProperties(new Color { Val = "0000FF" }), new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var good = Validate(Build(Compliant().Heading(1, "A")
            .Runs(Fuchsia("[wariant 1]"))
            .Runs(new Run(new Text("Kwota: ") { Space = SpaceProcessingModeValues.Preserve }), Blue("<kwota>"), new Run(new Text(" PLN ") { Space = SpaceProcessingModeValues.Preserve }), Fuchsia("[lub]"), new Run(new Text(" EUR") { Space = SpaceProcessingModeValues.Preserve }))
            .Runs(Fuchsia("[wariant 1.1]"))
            .Runs(Fuchsia("[koniec wariantu 1.1]"))
            .Runs(Fuchsia("[koniec wariantu 1]"))));
        Assert.DoesNotContain(good.Issues, i => i.Code.StartsWith("VARIANT") || i.Code == IssueCodes.VariableColorMismatch);

        var bad = Validate(Build(Compliant().Heading(1, "A")
            .Runs(Fuchsia("[wariant 1]"))
            .Runs(Fuchsia("[wariant 1.1]"))
            .Runs(Fuchsia("[koniec wariantu 1]"))
            .Runs(new Run(new Text("[wariant 2]")))
            .Runs(new Run(new Text("Data: <DD.MM.RRRR>")))
            .Runs(new Run(new Text("Zostałam [lub] em poinformowana.")))));

        var unbalanced = Assert.Single(bad.Errors, i => i.Code == IssueCodes.VariantMarkerUnbalanced);
        Assert.Contains("przecinające", unbalanced.Message);
        Assert.Contains("[wariant 2]", unbalanced.Message);
        Assert.Contains(bad.Errors, i => i.Code == IssueCodes.VariantMarkerColorMismatch);
        Assert.Contains(bad.Errors, i => i.Code == IssueCodes.VariableColorMismatch);
        Assert.Contains(bad.Errors, i => i.Code == IssueCodes.VariantInflectionShortcut);
    }

    [Fact]
    public void SampleMark_InBodyAndWrongStyle_G16()
    {
        var result = Validate(Build(Compliant().Heading(1, "A").Paragraph("WZÓR")));

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.SampleMarkNotOnEveryPage);
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.SampleMarkStyleMismatch);

        var inTable = Validate(Build(Compliant().Heading(1, "A").Paragraph("Tabela").Add(DataTable(t =>
            t.Elements<TableRow>().Last().Elements<TableCell>().First().Descendants<Text>().Single().Text = "WZÓR"))));
        Assert.DoesNotContain(inTable.Issues, i => i.Code is IssueCodes.SampleMarkStyleMismatch or IssueCodes.SampleMarkNotOnEveryPage);
    }

    [Fact]
    public void Lists_BulletColorAndBoldNumbers()
    {
        var document = Build(Compliant().Heading(1, "A")
            .Add(new Paragraph(new ParagraphProperties(new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 1 })), new Run(new Text("punkt"))))
            .Add(new Paragraph(new ParagraphProperties(new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 2 })), new Run(new Text("numer")))), main =>
        {
            var part = main.AddNewPart<NumberingDefinitionsPart>();
            part.Numbering = new Numbering(
                new AbstractNum(new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }, new LevelText { Val = "•" }, new NumberingSymbolRunProperties(new Color { Val = "0000FF" })) { LevelIndex = 0 }) { AbstractNumberId = 0 },
                new AbstractNum(new Level(new NumberingFormat { Val = NumberFormatValues.Decimal }, new LevelText { Val = "%1)" }) { LevelIndex = 0 }) { AbstractNumberId = 1 },
                new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = 1 },
                new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 2 });
        });

        var result = Validate(document);

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.ListBulletColorMismatch && i.Locations.Single().Snippet.Contains("#0000FF"));
        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.ListNumberNotBold);
    }

    [Fact]
    public void Table_WithoutHeaderRow_IsAnErrorOfStandard20_AndFixMarksIt_G08()
    {
        var table = DataTable(t => t.Elements<TableRow>().First().TableRowProperties!.RemoveAllChildren<TableHeader>());
        var document = Build(Compliant().Heading(1, "A").Paragraph("Tabela").Add(table));

        var result = Validate(document);
        var fixedResult = new WcagDocxFixer().Fix(document, Only20);

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TableMissingHeaders && i.Standard == "firmowy/2.0");
        Assert.DoesNotContain(fixedResult.After.Issues, i => i.Code == IssueCodes.TableMissingHeaders);
    }

    [Fact]
    public void Table_WithoutDescription_IsAnInfo()
    {
        var table = DataTable(t => t.GetFirstChild<TableProperties>()!.GetFirstChild<TableDescription>()!.Remove());

        var result = Validate(Build(Compliant().Heading(1, "A").Paragraph("Tabela").Add(table)));

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.TableDescriptionMissing);
        Assert.Equal(IssueSeverity.Info, issue.Severity);
    }

    [Fact]
    public void Salutation_At16pt_IsAllowed_ButOtherTextIsNot()
    {
        var result = Validate(Build(Compliant().Heading(1, "A")
            .Runs(new Run(new RunProperties(new FontSize { Val = "32" }), new Text("Szanowni Państwo,")))
            .Runs(new Run(new RunProperties(new FontSize { Val = "32" }), new Text("Zwykły akapit w 16 pt.")))));

        var issue = Assert.Single(result.Errors, i => i.Code == IssueCodes.BodyTextSizeMismatch);
        var location = Assert.Single(issue.Locations);
        Assert.Contains("Zwykły akapit", location.Snippet);
    }

    [Fact]
    public void Footer_LineSpacingOtherThan12_IsAnError_G11()
    {
        var result = Validate(Build(Compliant().Heading(1, "A"), main =>
            main.FooterParts.Single().Footer!.Elements<Paragraph>().Single().ParagraphProperties =
                new ParagraphProperties(new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Exact })));

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.LineSpacingMismatch);
        Assert.StartsWith("footer", issue.Locations.Single().Story);
    }

    [Fact]
    public void SlashAlternative_InsideVariant_IsAWarning_OutsideIsNot()
    {
        Run Fuchsia(string text) => new(new RunProperties(new Color { Val = "AB0066" }), new Text(text));

        var result = Validate(Build(Compliant().Heading(1, "A")
            .Paragraph("Stały tekst: PLN / EUR i adres example.org/regulamin.")
            .Runs(Fuchsia("[wariant 1]"))
            .Paragraph("Kwota w PLN / EUR / USD.")
            .Runs(Fuchsia("[koniec wariantu 1]"))));

        var issue = Assert.Single(result.Warnings, i => i.Code == IssueCodes.VariantSlashAlternative);
        var location = Assert.Single(issue.Locations);
        Assert.Contains("PLN / EUR", location.Snippet);
    }

    [Fact]
    public void TitlePage_WithH1At32pt_NeedsEmptyFirstPageFooter()
    {
        Paragraph TitleH1() => new(
            new ParagraphProperties(new ParagraphStyleId { Val = "Nagwek1" }),
            new Run(new RunProperties(new FontSize { Val = "64" }, new Color { Val = "FF6200" }), new Text("Regulamin")));

        var ordinaryFooter = Validate(Build(Compliant().Add(TitleH1())));
        Assert.Contains(ordinaryFooter.Warnings, i => i.Code == IssueCodes.TitlePageFooterPresent && i.Message.Contains("Inna na pierwszej stronie"));

        var emptyFirstFooter = Validate(Build(Compliant().Add(TitleH1()), main =>
        {
            var first = main.AddNewPart<FooterPart>();
            first.Footer = new Footer(new Paragraph());
            var section = main.Document.Body!.Elements<SectionProperties>().Single();
            section.PrependChild(new FooterReference { Type = HeaderFooterValues.First, Id = main.GetIdOfPart(first) });
            section.Append(new TitlePage());
        }));
        Assert.DoesNotContain(emptyFirstFooter.Issues, i => i.Code == IssueCodes.TitlePageFooterPresent);

        var standard24 = Validate(Build(Compliant().Heading(1, "Regulamin")));
        Assert.DoesNotContain(standard24.Issues, i => i.Code == IssueCodes.TitlePageFooterPresent);
    }

    [Fact]
    public void PaletteColor_UsedOutsideItsPurpose_IsAWarning_G02()
    {
        var result = Validate(Build(Compliant().Heading(1, "A")
            .Runs(new Run(new RunProperties(new Color { Val = "7724FF" }), new Text("fiolet poza łączem")))
            .Runs(new Run(new RunProperties(new Color { Val = "696969" }, new FontSize { Val = "16" }), new Text("dane rejestrowe 8 pt")))
            .Runs(new Run(new RunProperties(new Color { Val = "0000FF" }), new Text("paragraf niebieski")))));

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.TextColorUsageMismatch);
        var location = Assert.Single(issue.Locations);
        Assert.Contains("fiolet poza łączem", location.Snippet);
        Assert.DoesNotContain(result.Issues, i => i.Code == IssueCodes.TextColorNotAllowed);
    }

    [Fact]
    public void Logo_WithWrongAltTextOrSize_IsAWarning_G10()
    {
        var builder = Compliant().Heading(1, "A");
        var result = Validate(Build(builder.Image("Logo", name: "Logo banku")));

        var issue = Assert.Single(result.Warnings, i => i.Code == IssueCodes.BrandImageMismatch);
        var location = Assert.Single(issue.Locations);
        Assert.Contains($"„{Bank} logo”", location.Snippet);
        Assert.Contains("44 × 10 mm", location.Snippet);

        var fine = Validate(Build(Compliant().Heading(1, "A").Image("Zdjęcie oddziału", name: "Obraz 1")));
        Assert.DoesNotContain(fine.Issues, i => i.Code == IssueCodes.BrandImageMismatch);
    }

    [Fact]
    public void HierarchyAndLanguage_AreCheckedByStandard20_AndNotDuplicatedWithWcag()
    {
        var document = Build(Compliant().Heading(1, "A").Heading(3, "Przeskok"));

        var only20 = Validate(document);
        var both = new WcagDocxValidator().Validate(document, new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/2.0"));

        Assert.Contains(only20.Warnings, i => i.Code == IssueCodes.HeadingLevelSkipped);
        var merged = Assert.Single(both.Issues, i => i.Code == IssueCodes.HeadingLevelSkipped);
        Assert.Equal("wcag/2.1", merged.Standard);

        var noLanguage = Validate(Build(new DocxBuilder { Title = $"A. {Bank}", Author = Bank, PackageLanguage = null }.Heading(1, "A"), main =>
            main.StyleDefinitionsPart!.Styles!.DocDefaults!.RunPropertiesDefault!.RunPropertiesBaseStyle!.RemoveAllChildren<Languages>()));
        Assert.Contains(noLanguage.Errors, i => i.Code == IssueCodes.LanguageMissing);
    }
}
