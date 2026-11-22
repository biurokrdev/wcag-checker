using System.Globalization;
using D2.Wcag.Fixtures.Xlsx;

namespace D2.Wcag.SampleGenerator;

/// <summary>
/// Skoroszyt „Budżet działu” (fikcyjny dział fikcyjnej firmy) w dwóch wersjach: zgodnej (0 zgłoszeń w profilach WCAG 2.2
/// i 2.1) i niezgodnej (każda reguła XL-*, którą da się wywołać w pliku .xlsx). Zapis przez deterministyczny generator
/// fixtur XLSX (tests/D2.Wcag.Fixtures/Xlsx/FixtureXlsx.cs); obrazy podmieniane na grafiki z <see cref="SampleImages"/>.
/// </summary>
public static class BudgetWorkbook
{
    public const string Title = "Budżet Działu Administracji na 2026 rok";

    private const string SeriesDark = "1F3864";
    private const string SeriesOrange = "D2691E";

    private static readonly (string Category, double Plan, double Actual, string Status)[] Rows =
    [
        ("Wynagrodzenia", 420000, 415300, "Zatwierdzone"),
        ("Szkolenia", 36000, 21450, "Zatwierdzone"),
        ("Sprzęt komputerowy", 58000, 61200, "Do wyjaśnienia"),
        ("Materiały biurowe", 12500, 11870, "Zatwierdzone"),
        ("Podróże służbowe", 24000, 17640, "Wstrzymane"),
        ("Usługi zewnętrzne", 45000, 47900, "Do wyjaśnienia"),
    ];

    private static readonly (string Quarter, double Plan, double Actual)[] Quarters =
    [
        ("I kwartał", 148875, 141260),
        ("II kwartał", 148875, 152340),
        ("III kwartał", 148875, 139880),
        ("IV kwartał", 148875, 141880),
    ];

    public static IReadOnlyList<SampleProfile> NonCompliantProfiles { get; } =
    [
        new("WCAG 2.2", [], "2.2", Expected),
        new("WCAG 2.1", [], "2.1", Expected),
    ];

    // Zgłoszenia (błąd, ostrzeżenie, do weryfikacji, informacja nie wchodzi) w obu profilach.
    private static string[] Expected =>
    [
        "XL-ALT-MISSING", "XL-ALT-POOR", "XL-BLANK-LAYOUT", "XL-CHART-ALT", "XL-CHART-COLOR", "XL-CHART-CONTRAST", "XL-CHART-TITLE",
        "XL-CHART-TITLE-POOR", "XL-COLOR-ONLY", "XL-CONTRAST", "XL-CONTRAST-CF", "XL-CONTROL", "XL-DV-ERROR", "XL-DV-ERROR-TEXT", "XL-DV-INPUT",
        "XL-FIELD-LABEL", "XL-HIDDEN", "XL-LANG", "XL-LINK-TEXT", "XL-LINK-URL", "XL-MERGED", "XL-SENSORY", "XL-SHEET-NAME", "XL-START-A1",
        "XL-TABLE-HEADER", "XL-TABLE-HEADER-NAME", "XL-TABLE-MISSING", "XL-TEXTBOX", "XL-TITLE",
    ];

    public static byte[] Build(bool compliant) => compliant ? BuildCompliant() : BuildNonCompliant();

    // ------------------------------------------------------------ wersja zgodna

    private static byte[] BuildCompliant()
    {
        var fx = new FixtureXlsx
        {
            Title = Title,
            Subject = "Plan i wykonanie budżetu (dane przykładowe)",
            Language = "pl-PL",
            Description = "Dane przykładowe: firma, dział i kwoty są fikcyjne.",
        };
        var s = fx.Styles;
        var header = s.Xf(font: s.Font(bold: true), wrap: true);
        var money = s.Xf(numFmtCode: "#,##0 \"zł\"");
        var moneyBold = s.Xf(font: s.Font(bold: true), numFmtCode: "#,##0 \"zł\"");
        var percent = s.Xf(numFmtId: 10);
        var heading = s.Xf(font: s.Font(size: 14, bold: true));
        var wrap = s.Xf(wrap: true);
        var link = s.Xf(font: s.Font("rgb=\"FF0563C1\"", underline: true));

        // Arkusz 1: tabela budżetu od A1, wykres, obraz.
        var budget = fx.AddSheet("Budżet 2026");
        budget.FrozenRows = 1;
        budget.Columns(1, 1, 24).Columns(2, 4, 16).Columns(5, 5, 12).Columns(6, 6, 16);
        budget.Row("A1", header, "Kategoria kosztów", "Plan (zł)", "Wykonanie (zł)", "Różnica (zł)", "Wykonanie (%)", "Status");
        for (var i = 0; i < Rows.Length; i++)
        {
            var row = i + 2;
            var (category, plan, actual, status) = Rows[i];
            budget.Text($"A{row}", category)
                .Number($"B{row}", plan, money)
                .Number($"C{row}", actual, money)
                .Formula($"D{row}", $"B{row}-C{row}", Num(plan - actual), style: money)
                .Formula($"E{row}", $"C{row}/B{row}", Num(Math.Round(actual / plan, 6)), style: percent)
                .Text($"F{row}", status);
        }

        var last = Rows.Length + 1;
        budget.Table(new FxTable("TabelaBudzet", $"A1:F{last}", ["Kategoria kosztów", "Plan (zł)", "Wykonanie (zł)", "Różnica (zł)", "Wykonanie (%)", "Status"])
        {
            Style = "TableStyleMedium2",
            AltText = "Budżet Działu Administracji na 2026 rok",
            AltTextSummary = "Plan i wykonanie budżetu w sześciu kategoriach kosztów, z różnicą w złotych, procentem wykonania i statusem pozycji.",
        });
        budget.DataValidations.Add(
            "<dataValidation type=\"list\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" errorTitle=\"Niepoprawny status\" "
            + "error=\"Wybierz z listy jedną z wartości: Zatwierdzone, Wstrzymane albo Do wyjaśnienia.\" promptTitle=\"Status pozycji\" "
            + $"prompt=\"Wybierz z listy: Zatwierdzone, Wstrzymane albo Do wyjaśnienia.\" sqref=\"F2:F{last}\"><formula1>\"Zatwierdzone,Wstrzymane,Do wyjaśnienia\"</formula1></dataValidation>");
        budget.DataValidations.Add(
            "<dataValidation type=\"decimal\" operator=\"greaterThanOrEqual\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" "
            + "errorTitle=\"Niepoprawna kwota\" error=\"Wpisz kwotę w złotych jako liczbę nie mniejszą niż 0, np. 12500.\" "
            + "promptTitle=\"Kwota w złotych\" prompt=\"Wpisz kwotę w złotych bez spacji i symbolu waluty, np. 12500.\" "
            + $"sqref=\"B2:C{last}\"><formula1>0</formula1></dataValidation>");

        string[] categories = [.. Rows.Select(r => r.Category)];
        var chart = new FxChart
        {
            Title = "Plan i wykonanie budżetu według kategorii (zł)",
            CategoryAxisTitle = "Kategoria kosztów",
            ValueAxisTitle = "Kwota (zł)",
            PlotAreaFill = "FFFFFF",
        };
        chart.Series.Add(new FxSeries("Plan (zł)", "'Budżet 2026'!$B$1", $"'Budżet 2026'!$A$2:$A${last}", $"'Budżet 2026'!$B$2:$B${last}", categories,
            [.. Rows.Select(r => r.Plan)]) { Fill = SeriesDark });
        chart.Series.Add(new FxSeries("Wykonanie (zł)", "'Budżet 2026'!$C$1", $"'Budżet 2026'!$A$2:$A${last}", $"'Budżet 2026'!$C$2:$C${last}", categories,
            [.. Rows.Select(r => r.Actual)]) { Fill = SeriesOrange, Pattern = "wdUpDiag" });
        budget.Chart(chart, "Wykres budżetu", 2, "A10", "E32",
            "Wykres kolumnowy porównujący plan i wykonanie budżetu w sześciu kategoriach. Największa pozycja to wynagrodzenia: plan 420 000 zł, "
            + "wykonanie 415 300 zł. Wykonanie przekracza plan w kategoriach Sprzęt komputerowy i Usługi zewnętrzne.");
        budget.Image("Ikona budżetu", 3, "H2", "I7", description: "Ikona skarbonki – symbol budżetu działu", file: "skarbonka.png");

        // Arkusz 2: podsumowanie kwartalne jako druga tabela.
        var quarters = fx.AddSheet("Kwartały");
        quarters.FrozenRows = 1;
        quarters.Columns(1, 1, 16).Columns(2, 4, 18);
        quarters.Row("A1", header, "Kwartał", "Plan (zł)", "Wykonanie (zł)", "Różnica (zł)");
        for (var i = 0; i < Quarters.Length; i++)
        {
            var row = i + 2;
            var (quarter, plan, actual) = Quarters[i];
            quarters.Text($"A{row}", quarter).Number($"B{row}", plan, money).Number($"C{row}", actual, money)
                .Formula($"D{row}", $"B{row}-C{row}", Num(plan - actual), style: money);
        }

        quarters.Text("A6", "Razem", s.Xf(font: s.Font(bold: true)))
            .Formula("B6", "SUM(B2:B5)", Num(Quarters.Sum(q => q.Plan)), style: moneyBold)
            .Formula("C6", "SUM(C2:C5)", Num(Quarters.Sum(q => q.Actual)), style: moneyBold)
            .Formula("D6", "SUM(D2:D5)", Num(Quarters.Sum(q => q.Plan - q.Actual)), style: moneyBold);
        quarters.Table(new FxTable("TabelaKwartaly", "A1:D6", ["Kwartał", "Plan (zł)", "Wykonanie (zł)", "Różnica (zł)"])
        {
            Style = "TableStyleMedium2",
            AltText = "Budżet w podziale na kwartały",
            AltTextSummary = "Plan, wykonanie i różnica budżetu w czterech kwartałach 2026 roku; ostatni wiersz to suma roczna.",
        });

        // Arkusz 3: informacje, logo, opisowe łącze, oznaczenie danych przykładowych.
        var info = fx.AddSheet("Informacje");
        info.Columns(1, 1, 60).Columns(2, 2, 45).RowHeight(8, 18).RowHeight(9, 18).RowHeight(10, 18);
        info.Text("A1", "Informacje o skoroszycie", heading);
        info.Text("A2", $"Skoroszyt zawiera plan i wykonanie budżetu Działu Administracji firmy {FictionalData.Company} w roku 2026.", wrap);
        info.Text("A3", "Kwoty są w złotych. Ujemna różnica oznacza wykonanie wyższe niż plan – jest też opisana w kolumnie Status.", wrap);
        info.Text("A4", "Regulamin budżetowania (strona Example Company, PDF)", link);
        info.Hyperlink("A4", "https://www.example.com/regulamin-budzetowania.pdf");
        info.Text("A5", $"Kontakt: {FictionalData.Signatory}, {FictionalData.Department}, tel. {FictionalData.Phone}.", wrap);
        info.Text("A6", $"{FictionalData.Marker}: firma, dział, osoby i kwoty są fikcyjne i służą wyłącznie do testów.", wrap);
        info.Image("Logo", 2, "B8", "B11", description: $"{FictionalData.Company} logo", file: "logo.png");

        AddImages(fx);
        return fx.Build();
    }

    // ------------------------------------------------------------ wersja niezgodna

    private static byte[] BuildNonCompliant()
    {
        // Brak tytułu i języka we właściwościach (XL-TITLE, XL-LANG).
        var fx = new FixtureXlsx { Creator = FictionalData.Signatory };
        var s = fx.Styles;
        var bold = s.Xf(font: s.Font(bold: true));
        var money = s.Xf(numFmtCode: "#,##0 \"zł\"");
        var gray = s.Xf(font: s.Font("rgb=\"FFA6A6A6\""));
        var input = s.Xf(fill: s.Fill("rgb=\"FFFFF2CC\""), locked: false);
        var link = s.Xf(font: s.Font("rgb=\"FF0563C1\"", underline: true));

        // Arkusz 1 „Arkusz1”: budżet jako zwykły zakres (bez Tabeli Excela), scalenie w danych, pusty wiersz dzielący
        // dane, ukryty wiersz, szary tekst o słabym kontraście, formatowanie warunkowe samym kolorem.
        var budget = fx.AddSheet("Arkusz1");
        budget.Columns(1, 1, 24).Columns(2, 4, 16).Columns(5, 5, 30);
        budget.Row("A1", bold, "Kategoria kosztów", "Plan (zł)", "Wykonanie (zł)", "Różnica (zł)", "Uwagi");
        for (var i = 0; i < Rows.Length; i++)
        {
            // Wiersz 5 pusty (dzieli blok danych), dane od wiersza 2 do 8.
            var row = i < 3 ? i + 2 : i + 3;
            var (category, plan, actual, _) = Rows[i];
            budget.Text($"A{row}", category).Number($"B{row}", plan, money).Number($"C{row}", actual, money)
                .Formula($"D{row}", $"B{row}-C{row}", Num(plan - actual), style: money)
                .Text($"E{row}", plan >= actual ? "w planie" : "przekroczenie", gray);
        }

        budget.Text("B7", "korekta w toku").Merge("B7:C7");
        budget.HideRow(8);
        budget.Text("A10", "Pozycje zaznaczone na czerwono przekraczają plan, a wykres po prawej pokazuje szczegóły.");
        var redOnly = s.Dxf("<font><color rgb=\"FFFF0000\"/></font>");
        var whiteOnYellow = s.Dxf("<font><color rgb=\"FFFFFFFF\"/></font><fill><patternFill><bgColor rgb=\"FFFFFF00\"/></patternFill></fill>");
        budget.ConditionalFormats.Add(
            $"<conditionalFormatting sqref=\"D2:D9\"><cfRule type=\"cellIs\" dxfId=\"{redOnly}\" priority=\"1\" operator=\"lessThan\"><formula>0</formula></cfRule></conditionalFormatting>");
        budget.ConditionalFormats.Add(
            $"<conditionalFormatting sqref=\"E2:E9\"><cfRule type=\"containsText\" dxfId=\"{whiteOnYellow}\" priority=\"2\" operator=\"containsText\" text=\"przekroczenie\">"
            + "<formula>NOT(ISERROR(SEARCH(\"przekroczenie\",E2)))</formula></cfRule></conditionalFormatting>");

        string[] categories = [.. Rows.Take(3).Select(r => r.Category)];
        var plain = new FxChart { AutoTitleDeleted = true };
        plain.Series.Add(new FxSeries("Plan (zł)", "Arkusz1!$B$1", "Arkusz1!$A$2:$A$4", "Arkusz1!$B$2:$B$4", categories, [.. Rows.Take(3).Select(r => r.Plan)]) { Fill = "FF0000" });
        plain.Series.Add(new FxSeries("Wykonanie (zł)", "Arkusz1!$C$1", "Arkusz1!$A$2:$A$4", "Arkusz1!$C$2:$C$4", categories, [.. Rows.Take(3).Select(r => r.Actual)]) { Fill = "00B050" });
        budget.Chart(plain, "Wykres 1", 2, "G1", "N16");
        var generic = new FxChart { Title = "Wykres 2", CategoryAxisTitle = "Kategoria", ValueAxisTitle = "Kwota (zł)", PlotAreaFill = "FFFFFF" };
        generic.Series.Add(new FxSeries("Wykonanie (zł)", "Arkusz1!$C$1", "Arkusz1!$A$2:$A$4", "Arkusz1!$C$2:$C$4", categories, [.. Rows.Take(3).Select(r => r.Actual)]) { Fill = "FFFF00" });
        budget.Chart(generic, "Wykres 2", 3, "G18", "N33", "Wykres kolumnowy wykonania budżetu w trzech pierwszych kategoriach.");
        budget.Image("Obraz 4", 4, "A12", "B16", file: "logo.png");
        budget.Image("Obraz 5", 5, "C12", "C16", description: "Obraz 5", file: "skarbonka.png");
        budget.Shape("pole tekstowe 6", 6, "A18", "E22", "Uwaga: kwoty za grudzień są szacunkowe i zostaną skorygowane w styczniu.", textBox: true);

        // Arkusz 2: formularz zgłoszenia wydatku – chroniony, pola do wypełnienia.
        var form = fx.AddSheet("Wniosek o wydatek");
        form.Protected = true;
        form.Columns(1, 2, 28);
        form.Text("A1", "Wniosek o wydatek z budżetu działu", bold);
        form.Text("A3", "Kategoria kosztów").Styled("B3", input);
        form.Text("A4", "Kwota (zł)").Styled("B4", input);
        form.Text("A5", "Data wydatku").Styled("B5", input);
        form.Styled("D8", input).Styled("D9", input).Styled("F3", input).Styled("F4", input);
        form.DataValidations.Add("<dataValidation type=\"list\" allowBlank=\"1\" showErrorMessage=\"1\" sqref=\"B3\"><formula1>\"Szkolenia,Sprzęt komputerowy,Materiały biurowe\"</formula1></dataValidation>");
        form.DataValidations.Add(
            "<dataValidation type=\"decimal\" operator=\"greaterThan\" allowBlank=\"1\" showErrorMessage=\"1\" error=\"Błąd\" sqref=\"B4\"><formula1>0</formula1></dataValidation>");
        form.DataValidations.Add("<dataValidation type=\"decimal\" operator=\"greaterThanOrEqual\" allowBlank=\"1\" sqref=\"D8:D9\"><formula1>0</formula1></dataValidation>");
        form.FormControl("Check Box 1", 1025, "CheckBox", "A7", "B7");

        // Arkusz 3: tabele pomocnicze – bez wiersza nagłówka i z nagłówkami domyślnymi.
        var helper = fx.AddSheet("Tabele pomocnicze");
        helper.Columns(1, 1, 16).Columns(2, 3, 14).Columns(5, 5, 20).Columns(6, 6, 14);
        for (var i = 0; i < Quarters.Length; i++)
        {
            var row = i + 1;
            helper.Text($"A{row}", Quarters[i].Quarter).Number($"B{row}", Quarters[i].Plan, money).Number($"C{row}", Quarters[i].Actual, money);
        }

        helper.Table(new FxTable("Tabela1", "A1:C4", ["Kolumna1", "Kolumna2", "Kolumna3"]) { HeaderRow = false });
        helper.Row("E1", 0, "Kolumna1", "Kolumna2");
        helper.Text("E2", "Szkolenia").Number("F2", 21450, money).Text("E3", "Podróże służbowe").Number("F3", 17640, money);
        helper.Table(new FxTable("Tabela2", "E1:F3", ["Kolumna1", "Kolumna2"]));

        // Arkusz 4: łącza – ogólny tekst i surowy adres.
        var links = fx.AddSheet("Łącza");
        links.Columns(1, 1, 60);
        links.Text("A1", "Dokumenty", bold);
        links.Text("A2", "kliknij tutaj", link).Hyperlink("A2", "https://www.example.com/regulamin-budzetowania.pdf");
        links.Text("A3", "https://www.example.com/raporty/budzet-2026", link).Hyperlink("A3", "https://www.example.com/raporty/budzet-2026");

        // Arkusz 5: archiwum zaczynające się daleko od A1.
        var archive = fx.AddSheet("Archiwum 2025");
        archive.Columns(8, 9, 22);
        archive.Row("H20", bold, "Kategoria", "Wykonanie 2025 (zł)");
        archive.Text("H21", "Wynagrodzenia").Number("I21", 401200, money).Text("H22", "Szkolenia").Number("I22", 29800, money);
        archive.Table(new FxTable("TabelaArchiwum", "H20:I22", ["Kategoria", "Wykonanie 2025 (zł)"]));

        // Arkusz 6: ukryty słownik z danymi.
        var hidden = fx.AddSheet("Stawki", "hidden");
        hidden.Row("A1", 0, "Stanowisko", "Stawka (zł/h)");
        hidden.Text("A2", "Specjalista").Number("B2", 85).Text("A3", "Kierownik").Number("B3", 120);

        AddImages(fx);
        return fx.Build();
    }

    // ------------------------------------------------------------ pomocnicze

    private static void AddImages(FixtureXlsx fx)
    {
        fx.ExtraParts["xl/media/logo.png"] = (SampleImages.Logo(), null);
        fx.ExtraParts["xl/media/skarbonka.png"] = (SampleImages.BudgetIcon(), null);
    }

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
