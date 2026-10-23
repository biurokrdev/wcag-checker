using System.Globalization;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Api.Rendering;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace D2.Wcag.Api.Reports;

public sealed record ReportExportOptions(IReadOnlySet<int> Verified, bool IncludePreviews, string? Reviewer, int MaxPreviews = 100);

// Raport walidacji jako DOCX zbudowany OpenXml-em: metadane, style nagłówków, tabele z wierszem nagłówkowym i podpisem,
// obrazy z tekstem alternatywnym, stopka z numerem strony, język pl-PL. Czyli taki, jaki sam przechodzi walidację WCAG.
public static class ReportDocxWriter
{
    private const string FontName = "Arial";
    private const int MaxImageWidthEmu = 6_000_000;
    private const long EmuPerPixel = 9525;

    public static byte[] Write(StoredReport report, ReportExportOptions options)
    {
        var response = report.Response;
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var body = new Body();
            main.Document = new Document(body);
            AddStyles(main);

            body.Append(Heading(1, $"Raport walidacji dokumentu „{report.FileName}”"));
            body.Append(Paragraph($"Wynik: {(response.Status == ValidationOutcome.Valid ? "poprawny" : "niepoprawny")}. {response.Summary}", bold: true));
            body.Append(Paragraph($"Data sprawdzenia: {report.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}. Standardy: {string.Join("; ", response.Standards.Select(s => s.Name + (s.Confirmed ? "" : " (wersja robocza)")))}."));
            if (!string.IsNullOrWhiteSpace(options.Reviewer))
            {
                body.Append(Paragraph($"Weryfikacja ręczna: {options.Reviewer.Trim()}."));
            }

            var d = response.Document;
            body.Append(Paragraph($"Dokument: tytuł „{d.Title ?? "brak"}”, autor „{d.Author ?? "brak"}”, język {d.Language ?? "brak"}, {d.Paragraphs} akapitów, {d.Headings} nagłówków, {d.Tables} tabel, {d.Images} obrazów."));

            body.Append(Heading(2, "Podsumowanie kontroli"));
            foreach (var standard in response.SummaryTable.GroupBy(s => s.Standard))
            {
                body.Append(Heading(3, standard.First().StandardName));
                foreach (var section in standard)
                {
                    var caption = $"Sekcja „{section.Section}”";
                    body.Append(Paragraph(caption));
                    body.Append(Table(caption, ["Status", "Kontrola", "Szczegóły"], section.Rows.Select(r => new[]
                    {
                        StatusLabel(r.Status),
                        r.Name,
                        r.Details.Count == 0
                            ? r.Note ?? ""
                            : string.Join("\n", r.Details.Select(x => $"{x.Label}: {x.Message}{(x.Occurrences > 1 ? $" ({x.Occurrences} wystąpień)" : "")}")),
                    }).ToList(), [2000, 3000, 4360]));
                }
            }

            if (response.Wcag is { } wcag)
            {
                body.Append(Heading(2, "Kryteria WCAG 2.1"));
                body.Append(Paragraph(wcag.Summary));
                body.Append(Paragraph("Tabela stanu kryteriów WCAG 2.1"));
                body.Append(Table("Stan kryteriów WCAG 2.1", ["Kryterium", "Poziom", "Pokrycie", "Stan", "Uwagi"], wcag.Criteria.Select(c => new[]
                {
                    $"{c.Id} {c.Name}",
                    c.Level,
                    CoverageLabel(c.Coverage),
                    CriterionLabel(c.Status),
                    c.Issues.Count == 0 ? c.Note : string.Join("\n", c.Issues.Select(i => $"{i.Label}: {i.Message}")),
                }).ToList(), [2600, 900, 1300, 1400, 3160]));
            }

            body.Append(Heading(2, "Wyniki walidacji"));
            var verifiedCount = response.Findings.Count(f => options.Verified.Contains(f.Index));
            body.Append(Paragraph($"{response.Findings.Count} wystąpień, w tym {verifiedCount} oznaczonych jako zweryfikowane."));
            body.Append(Paragraph("Tabela wystąpień"));
            body.Append(Table("Wystąpienia zgłoszeń", ["Nr", "Waga", "Zgłoszenie", "Standard", "Miejsce", "Komunikat", "Zweryfikowano"], response.Findings.Select(f => new[]
            {
                (f.Index + 1).ToString(CultureInfo.InvariantCulture),
                SeverityLabel(f.Severity),
                f.Label,
                f.StandardName ?? f.Standard ?? "",
                f.Location is { } l ? $"{KindLabel(l.Kind)}: „{l.Snippet}”{(f.OccurrenceCount > 1 ? $" ({f.Occurrence}/{f.OccurrenceCount})" : "")}" : "cały dokument",
                f.Message,
                options.Verified.Contains(f.Index) ? "tak" : "nie",
            }).ToList(), [500, 1100, 1900, 1500, 2000, 1860, 500]));

            if (options.IncludePreviews)
            {
                AppendPreviews(main, body, report, options);
            }

            AddFooterAndSection(main, body);

            package.PackageProperties.Title = $"Raport walidacji: {report.FileName}";
            package.PackageProperties.Creator = string.IsNullOrWhiteSpace(options.Reviewer) ? "D2.Wcag" : $"{options.Reviewer.Trim()} (D2.Wcag)";
            package.PackageProperties.Language = "pl-PL";
            package.PackageProperties.Created = report.CreatedAt.UtcDateTime;
        }

        return stream.ToArray();
    }

    private static void AppendPreviews(MainDocumentPart main, Body body, StoredReport report, ReportExportOptions options)
    {
        var located = report.Findings.Where(f => f.Location is { Kind: not LocationKind.Document }).Take(options.MaxPreviews).ToList();
        if (located.Count == 0)
        {
            return;
        }

        body.Append(Heading(2, "Miejsca wystąpień"));
        body.Append(Paragraph($"Wycinki schematu strony z zaznaczonym elementem dla {located.Count} wystąpień{(report.Findings.Count(f => f.Location is { Kind: not LocationKind.Document }) > located.Count ? $" (pierwszych {options.MaxPreviews})" : "")}. Schemat nie odwzorowuje wiernie Worda, ale zaznaczenie wskazuje dokładnie ten element."));

        using var renderer = new SchematicDocxRenderer(report.Document);
        foreach (var finding in located)
        {
            var location = finding.Location!;
            var preview = IssuePreviewRenderer.Render(renderer, new IssueLocation(location.Kind, location.Story, location.Index, location.SubIndex, location.Snippet), includePage: false);
            body.Append(Heading(3, $"Nr {finding.Index + 1}: {finding.Label}{(options.Verified.Contains(finding.Index) ? " (zweryfikowano)" : "")}"));
            body.Append(Paragraph(finding.Message));
            body.Append(Paragraph($"{preview.Note}"));
            if (preview.Zoom is { } zoom)
            {
                body.Append(Image(main, zoom, $"Wycinek strony {preview.Page} z {preview.PageCount} z zaznaczonym elementem: {location.Snippet}"));
            }
        }
    }

    // ---- elementy

    private static Paragraph Heading(int level, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = $"Heading{level}" }), new Run(new Text(text)));

    private static Paragraph Paragraph(string text, bool bold = false)
    {
        var paragraph = new Paragraph();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var run = new Run();
            if (bold)
            {
                run.RunProperties = new RunProperties(new Bold());
            }

            if (i > 0)
            {
                run.Append(new Break());
            }

            run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(run);
        }

        return paragraph;
    }

    private static Table Table(string caption, string[] headers, IReadOnlyList<string[]> rows, int[] widthsTwips)
    {
        var table = new Table(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 }, new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 }, new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
            new TableCaption { Val = caption }));

        var grid = new TableGrid();
        foreach (var width in widthsTwips)
        {
            grid.Append(new GridColumn { Width = width.ToString(CultureInfo.InvariantCulture) });
        }

        table.Append(grid);

        var header = new TableRow(new TableRowProperties(new TableHeader()));
        for (var i = 0; i < headers.Length; i++)
        {
            header.Append(Cell(headers[i], widthsTwips[i], bold: true, shading: "E8E8E8"));
        }

        table.Append(header);
        foreach (var row in rows)
        {
            var tableRow = new TableRow();
            for (var i = 0; i < row.Length; i++)
            {
                tableRow.Append(Cell(row[i], widthsTwips[i]));
            }

            table.Append(tableRow);
        }

        return table;
    }

    private static TableCell Cell(string text, int widthTwips, bool bold = false, string? shading = null)
    {
        var properties = new TableCellProperties(new TableCellWidth { Width = widthTwips.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa });
        if (shading is not null)
        {
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = shading, Color = "auto" });
        }

        return new TableCell(properties, Paragraph(text.Length == 0 ? "–" : text, bold));
    }

    private static Paragraph Image(MainDocumentPart main, PreviewImage image, string altText)
    {
        var part = main.AddImagePart(ImagePartType.Jpeg);
        part.FeedData(new MemoryStream(image.Jpeg));
        var relationshipId = main.GetIdOfPart(part);

        var widthEmu = image.Width * EmuPerPixel;
        var heightEmu = image.Height * EmuPerPixel;
        if (widthEmu > MaxImageWidthEmu)
        {
            heightEmu = heightEmu * MaxImageWidthEmu / widthEmu;
            widthEmu = MaxImageWidthEmu;
        }

        var id = (uint)main.ImageParts.Count();
        var inline = new DW.Inline(
            new DW.Extent { Cx = widthEmu, Cy = heightEmu },
            new DW.DocProperties { Id = id, Name = $"Podgląd {id}", Description = altText },
            new A.Graphic(new A.GraphicData(new PIC.Picture(
                new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = 0U, Name = $"podglad{id}.jpg" }, new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            {
                Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
            }))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
        };

        return new Paragraph(new Run(new Drawing(inline)));
    }

    private static void AddFooterAndSection(MainDocumentPart main, Body body)
    {
        var footer = main.AddNewPart<FooterPart>();
        footer.Footer = new Footer(new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(new Text("Strona ") { Space = SpaceProcessingModeValues.Preserve }),
            new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " }));

        body.Append(new SectionProperties(
            new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin { Top = 1134, Bottom = 1134, Left = 1134U, Right = 1134U, Header = 708U, Footer = 708U, Gutter = 0U }));
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName },
                    new FontSize { Val = "20" },
                    new Languages { Val = "pl-PL" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(new SpacingBetweenLines { After = "120", Line = "276", LineRule = LineSpacingRuleValues.Auto }))),
            new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
            HeadingStyle(1, "32", "1F3864"),
            HeadingStyle(2, "26", "1F3864"),
            HeadingStyle(3, "22", "1F3864"));

        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = styles;
    }

    private static Style HeadingStyle(int level, string sizeHalfPoints, string color) => new(
        new StyleName { Val = $"heading {level}" },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new PrimaryStyle(),
        new StyleParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = level == 1 ? "240" : "200", After = "80" }, new OutlineLevel { Val = level - 1 }),
        new StyleRunProperties(new Bold(), new Color { Val = color }, new FontSize { Val = sizeHalfPoints }))
    {
        Type = StyleValues.Paragraph,
        StyleId = $"Heading{level}",
    };

    private static string StatusLabel(CheckStatus status) => status switch
    {
        CheckStatus.Passed => "Zatwierdzono",
        CheckStatus.Error => "Błąd",
        CheckStatus.Warning => "Ostrzeżenie",
        CheckStatus.Info => "Informacja",
        _ => "Ręczna weryfikacja",
    };

    private static string CriterionLabel(CriterionStatus status) => status switch
    {
        CriterionStatus.Passed => "spełnione",
        CriterionStatus.Failed => "niespełnione",
        CriterionStatus.NeedsReview => "do przeglądu",
        _ => "niesprawdzone",
    };

    private static string CoverageLabel(CriterionCoverage coverage) => coverage switch
    {
        CriterionCoverage.Automatic => "automatycznie",
        CriterionCoverage.Partial => "częściowo",
        _ => "ręcznie",
    };

    private static string SeverityLabel(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Error => "błąd",
        IssueSeverity.Warning => "ostrzeżenie",
        _ => "informacja",
    };

    private static string KindLabel(LocationKind kind) => kind switch
    {
        LocationKind.Paragraph => "akapit",
        LocationKind.Run => "fragment tekstu",
        LocationKind.Table => "tabela",
        LocationKind.TableCell => "komórka tabeli",
        LocationKind.Drawing => "obraz",
        LocationKind.Hyperlink => "łącze",
        _ => "dokument",
    };
}
