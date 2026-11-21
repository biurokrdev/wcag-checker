using System.Globalization;
using D2.Wcag.Xlsx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Api.Workbooks;

// Raport audytu skoroszytu jako DOCX zbudowany OpenXml-em: metadane (tytuł, język), style nagłówków, tabele z wierszem
// nagłówkowym i podpisem, stopka stron z zastrzeżeniem i numerem strony – czyli dokument, który sam przechodzi
// walidację WCAG biblioteki. Raport nigdy nie stwierdza zgodności: zastrzeżenie stoi w treści (na początku i na końcu)
// oraz w stopce każdej strony. Osobny od ReportDocxWriter (Word), którego nie zmienia.
public static class WorkbookReportDocxWriter
{
    private const string FontName = "Arial";

    internal const string ScopeNote =
        "Raport z automatycznej analizy pliku; nie zastępuje audytu dostępności ani testów z technologiami wspomagającymi. "
        + "Interpretacja kryteriów dla dokumentów nie-webowych wg noty informacyjnej W3C WCAG2ICT.";

    internal const string SoftwareNote = "Zależy od programu użytego do otwarcia pliku; nie oceniano.";

    internal const string ChecklistNote =
        "Odhaczenie pozycji oznacza, że osoba sprawdzająca ją przejrzała. Nie zmienia statusów kryteriów ani podsumowania.";

    public static string Title(WorkbookValidationResponse response) => $"Raport audytu dostępności: {response.File.FileName}";

    public static byte[] Write(StoredWorkbookReport report, IReadOnlySet<string> checkedManual)
    {
        var r = report.Response;
        var disclaimer = Sentence(r.Disclaimer);
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var body = new Body();
            main.Document = new Document(body);
            AddStyles(main);

            body.Append(Heading(1, $"Raport audytu dostępności skoroszytu „{r.File.FileName}”"));
            body.Append(Paragraph(r.Summary, bold: true));
            body.Append(Paragraph(disclaimer, bold: true));
            body.Append(Paragraph(
                $"Plik: {r.File.FileName} ({r.File.FormatName}). Profil: {r.Profile.ProfileName}. Data analizy: {report.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}."));
            body.Append(Paragraph(ScopeNote));

            var w = r.Workbook;
            body.Append(Paragraph(
                $"Skoroszyt: tytuł „{w.Title ?? "brak"}”, język {w.Language ?? "brak"}, arkusze: {w.Sheets} (w tym ukryte: {w.HiddenSheets}), "
                + $"komórki: {w.Cells}, tabele: {w.Tables}, obrazy: {w.Images}, wykresy: {w.Charts}."));
            if (w.IsEmpty)
            {
                body.Append(Paragraph("Skoroszyt jest pusty – nie ma treści do oceny."));
            }

            if (w.HasMacros)
            {
                body.Append(Paragraph("Skoroszyt zawiera makra. Makra nie są uruchamiane ani oceniane przez audyt."));
            }

            AppendCounts(body, r, checkedManual);
            AppendFindings(body, r);
            AppendCriteria(body, r);
            AppendChecklist(body, r, checkedManual);

            body.Append(Heading(2, "Zastrzeżenie"));
            body.Append(Paragraph(disclaimer, bold: true));
            body.Append(Paragraph(ScopeNote));

            AddFooterAndSection(main, body, disclaimer);

            package.PackageProperties.Title = Title(r);
            package.PackageProperties.Creator = "D2.Wcag";
            package.PackageProperties.Language = "pl-PL";
            package.PackageProperties.Created = report.CreatedAt.UtcDateTime;
        }

        return stream.ToArray();
    }

    // ---- sekcje

    private static void AppendCounts(Body body, WorkbookValidationResponse r, IReadOnlySet<string> checkedManual)
    {
        body.Append(Heading(2, "Podsumowanie liczbowe"));
        const string caption = "Liczba zgłoszeń według wagi i liczba pozycji checklisty";
        body.Append(Paragraph(caption));
        body.Append(Table(caption, ["Kategoria", "Liczba"],
        [
            ["Błędy (kontrola automatyczna, kryterium poziomu A lub AA)", Number(r.Counts.Errors)],
            ["Ostrzeżenia (heurystyka o wysokiej pewności)", Number(r.Counts.Warnings)],
            ["Do weryfikacji (heurystyka o niższej pewności)", Number(r.Counts.NeedsReview)],
            ["Informacje (dobre praktyki, nie wymóg WCAG)", Number(r.Counts.Info)],
            ["Pozycje checklisty do oceny ręcznej", $"{r.ManualChecks.Count} (odhaczono {checkedManual.Count} z {r.ManualChecks.Count})"],
        ], [7000, 2360]));
    }

    private static void AppendFindings(Body body, WorkbookValidationResponse r)
    {
        body.Append(Heading(2, "Zgłoszenia"));
        if (r.Findings.Count == 0)
        {
            body.Append(Paragraph("Kontrole automatyczne i heurystyczne nie zgłosiły żadnych pozycji. Kryteria oznaczone „Do oceny ręcznej” nadal wymagają sprawdzenia przez człowieka."));
            return;
        }

        body.Append(Paragraph($"Zgłoszenia pogrupowane według arkusza: {r.Findings.Count}. Błąd pochodzi z kontroli automatycznej; ostrzeżenie i pozycja „do weryfikacji” to możliwy problem, który wymaga potwierdzenia przez człowieka."));

        // Najpierw zgłoszenia dotyczące całego skoroszytu, potem arkusze w kolejności skoroszytu; w grupie – kolejność wyniku.
        var groups = r.Findings
            .GroupBy(f => f.Location.Sheet, StringComparer.Ordinal)
            .OrderBy(g => g.Key is null ? -1 : g.Min(f => f.Location.SheetIndex ?? int.MaxValue))
            .ThenBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var title = group.Key is null ? "Skoroszyt" : $"Arkusz „{group.Key}”";
            body.Append(Heading(3, title));
            var caption = group.Key is null ? "Zgłoszenia dotyczące całego skoroszytu" : $"Zgłoszenia na arkuszu „{group.Key}”";
            body.Append(Paragraph(caption));
            body.Append(Table(caption, ["Nr", "Waga", "Zgłoszenie", "Kryterium", "Miejsce", "Opis i dowód", "Jak naprawić"],
                group.Select(FindingRow).ToList(), [500, 1100, 1700, 1300, 1500, 1830, 1430]));
        }
    }

    private static string[] FindingRow(WorkbookFindingDto f)
    {
        var what = f.Title + "\n" + f.CheckTypeLabel
            + (f.Confidence is { } confidence ? $" (pewność {confidence.ToString("P0", CultureInfo.GetCultureInfo("pl-PL"))})" : "")
            + "\nKod: " + f.Code;
        var criteria = f.BestPractice || f.Wcag.Count == 0
            ? XlsxFinding.BestPracticeLabel
            : string.Join("\n", f.Wcag.Select(Criterion));
        var place = f.Location.Display + (f.Location.Snippet is null ? "" : $"\n„{f.Location.Snippet}”");
        var description = f.Message
            + (f.Evidence.Count == 0 ? "" : "\nDowód: " + string.Join("; ", f.Evidence.Select(e => $"{e.Name}: {e.Value}")))
            + (f.MoreCount > 0 ? $"\n+{f.MoreCount} kolejnych wystąpień tej reguły na tym arkuszu." : "");
        return [Number(f.Index + 1), f.SeverityLabel, what, criteria, place, description, f.FixInstruction];
    }

    private static void AppendCriteria(Body body, WorkbookValidationResponse r)
    {
        var version = r.Profile.WcagVersion;
        var own = r.Criteria.Where(c => !c.SoftwareDependent).ToList();
        var software = r.Criteria.Where(c => c.SoftwareDependent).ToList();

        body.Append(Heading(2, $"Macierz kryteriów WCAG {version} (poziomy A i AA)"));
        body.Append(Paragraph(
            "„Brak wykrytych problemów” oznacza wyłącznie, że kontrole automatyczne nic nie zgłosiły – nie jest potwierdzeniem spełnienia kryterium. "
            + "„Nie dotyczy” oznacza, że kryterium nie ma zastosowania do zawartości tego pliku (w modelu WCAG: spełnione automatycznie)."));
        body.Append(Paragraph(string.Join("; ", own.GroupBy(c => c.Status).OrderBy(g => g.Key).Select(g => $"{g.First().StatusLabel}: {g.Count()}")) + "."));
        var caption = $"Stan kryteriów WCAG {version} po analizie automatycznej";
        body.Append(Paragraph(caption));
        body.Append(Table(caption, ["Kryterium", "Poziom", "Status", "Uzasadnienie"], own.Select(c => new[]
        {
            $"{c.Sc} {c.Name}",
            c.Level,
            c.StatusLabel,
            c.Reason + (c.Codes.Count == 0 ? "" : $"\nReguły ze zgłoszeniami: {string.Join(", ", c.Codes)}.")
                + (c.ManualChecks > 0 ? $"\nPozycje checklisty: {c.ManualChecks}." : ""),
        }).ToList(), [2600, 800, 1700, 4260]));

        if (software.Count == 0)
        {
            return;
        }

        body.Append(Heading(2, "Kryteria zależne od oprogramowania"));
        body.Append(Paragraph("Te kryteria spełnia (lub nie) program użyty do otwarcia pliku, np. Excel, a nie autor skoroszytu. Nie zostały ocenione."));
        const string softwareCaption = "Kryteria zależne od programu użytego do otwarcia pliku";
        body.Append(Paragraph(softwareCaption));
        body.Append(Table(softwareCaption, ["Kryterium", "Poziom", "Adnotacja", "Uzasadnienie"], software.Select(c => new[]
        {
            $"{c.Sc} {c.Name}",
            c.Level,
            c.Status == XlsxCriterionStatus.NotApplicable ? SoftwareNote : $"{c.StatusLabel}\n{SoftwareNote}",
            c.Reason,
        }).ToList(), [2600, 800, 1700, 4260]));
    }

    private static void AppendChecklist(Body body, WorkbookValidationResponse r, IReadOnlySet<string> checkedManual)
    {
        body.Append(Heading(2, "Checklista oceny ręcznej"));
        if (r.ManualChecks.Count == 0)
        {
            body.Append(Paragraph("Brak pozycji do oceny ręcznej."));
            return;
        }

        body.Append(Paragraph($"Odhaczono {checkedManual.Count} z {r.ManualChecks.Count}. {ChecklistNote}"));
        const string caption = "Pytania do oceny przez człowieka";
        body.Append(Paragraph(caption));
        body.Append(Table(caption, ["Nr", "Kryterium", "Pytanie", "Jak sprawdzić", "Odhaczono"], r.ManualChecks.Select((c, i) => new[]
        {
            Number(i + 1),
            c.Wcag.Count == 0 ? XlsxFinding.BestPracticeLabel : string.Join("\n", c.Wcag.Select(Criterion)),
            c.Question + (c.Location is null ? "" : $"\nMiejsce: {c.Location.Display}"),
            c.Hint ?? "",
            checkedManual.Contains(c.Id) ? "tak" : "nie",
        }).ToList(), [500, 1800, 3400, 2660, 1000]));
    }

    private static string Criterion(WorkbookWcagRefDto reference) =>
        $"{reference.Sc}{(reference.Name is null ? "" : " " + reference.Name)} ({reference.Level})";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Sentence(string text) => text.EndsWith('.') ? text : text + ".";

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

    // Stopka każdej strony: zastrzeżenie i numer strony.
    private static void AddFooterAndSection(MainDocumentPart main, Body body, string disclaimer)
    {
        var footer = main.AddNewPart<FooterPart>();
        footer.Footer = new Footer(new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(new Text(disclaimer + " Strona ") { Space = SpaceProcessingModeValues.Preserve }),
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
}
