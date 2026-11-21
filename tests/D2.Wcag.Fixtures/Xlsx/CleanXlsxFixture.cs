namespace D2.Wcag.Fixtures.Xlsx;

// clean.xlsx – skoroszyt wzorcowy: nie może prowokować żadnej reguły (0 błędów i 0 ostrzeżeń z każdej grupy).
//
// Właściwości: tytuł „Budżet działu na 2026 rok”, język pl-PL, autor Example Company S. A.
//
// Arkusz 1 „Budżet 2026” (zamrożony wiersz nagłówka, treść od A1):
//   A1:D5   tabela „TabelaBudzet” z wierszem nagłówka: Kategoria | Plan (zł) | Wykonanie (zł) | Status;
//           tekst alternatywny tabeli (tytuł i opis); nagłówki pogrubione; kwoty w formacie #,##0.00; styl TableStyleLight1
//   D2:D5   walidacja danych (lista „Zatwierdzone,Wstrzymane”) z komunikatem wejściowym i komunikatem o błędzie
//   F2:G5   obraz „Logo” (id 2) z tekstem alternatywnym „Logo Example Company”
//   F7:M22  wykres kolumnowy „Wykres budżetu” (id 3): tytuł, tytuły obu osi, opis alternatywny, legenda, białe tło obszaru
//           kreślenia; 2 serie: „Plan (zł)” 1F3864 pełnym kolorem, „Wykonanie (zł)” D2691E wzorem (wdUpDiag) i z etykietami
//           danych – serie różnią się nie tylko kolorem, a kolory mają kontrast ≥ 3:1 do tła i między sobą
// Arkusz 2 „Informacje”:
//   A1      nagłówek „Informacje o skoroszycie” (14 pt, pogrubiony)
//   A2, A4  zwykły tekst
//   A3      opisowe łącze „Regulamin budżetowania (strona Example Company)” → https://example.com/regulamin-budzetowania
//           (czcionka 0563C1 podkreślona, kontrast do bieli ok. 5,9:1)
//   C1:D3   obraz dekoracyjny „Ozdobnik” (id 2): adec:decorative, pusty descr
//
// Wszystkie czcionki czarne (motyw dk1) na białym tle, poza łączem. Brak: scaleń, ukrytych arkuszy/wierszy/kolumn,
// komentarzy, kontrolek, formatowania warunkowego, makr.
public static class CleanXlsxFixture
{
    public const string SeriesDark = "1F3864";
    public const string SeriesOrange = "D2691E";

    public static byte[] Build()
    {
        var fx = new FixtureXlsx { Title = "Budżet działu na 2026 rok", Language = "pl-PL" };
        var bold = fx.Styles.Xf(font: fx.Styles.Font(bold: true));
        var money = fx.Styles.Xf(numFmtId: 4);
        var heading = fx.Styles.Xf(font: fx.Styles.Font(size: 14, bold: true));
        var link = fx.Styles.Xf(font: fx.Styles.Font("rgb=\"FF0563C1\"", underline: true));

        var budget = fx.AddSheet("Budżet 2026");
        budget.FrozenRows = 1;
        budget.Columns(1, 4, 18);
        budget.Row("A1", bold, "Kategoria", "Plan (zł)", "Wykonanie (zł)", "Status");
        (string Category, double Plan, double Actual, string Status)[] rows =
        [
            ("Wynagrodzenia", 120000, 118500, "Zatwierdzone"),
            ("Szkolenia", 15000, 9800, "Wstrzymane"),
            ("Sprzęt", 40000, 41250, "Zatwierdzone"),
            ("Podróże służbowe", 8000, 6100, "Zatwierdzone"),
        ];
        for (var i = 0; i < rows.Length; i++)
        {
            var row = i + 2;
            budget.Text($"A{row}", rows[i].Category).Number($"B{row}", rows[i].Plan, money).Number($"C{row}", rows[i].Actual, money)
                .Text($"D{row}", rows[i].Status);
        }

        budget.Table(new FxTable("TabelaBudzet", "A1:D5", ["Kategoria", "Plan (zł)", "Wykonanie (zł)", "Status"])
        {
            AltText = "Budżet działu na 2026 rok",
            AltTextSummary = "Plan i wykonanie budżetu w podziale na kategorie kosztów, ze statusem zatwierdzenia.",
        });
        budget.DataValidations.Add(
            "<dataValidation type=\"list\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" errorTitle=\"Niepoprawny status\" "
            + "error=\"Wybierz z listy jedną z wartości: Zatwierdzone albo Wstrzymane.\" promptTitle=\"Status pozycji\" "
            + "prompt=\"Wybierz z listy: Zatwierdzone albo Wstrzymane.\" sqref=\"D2:D5\"><formula1>\"Zatwierdzone,Wstrzymane\"</formula1></dataValidation>");
        budget.Image("Logo", 2, "F2", "G5", description: "Logo Example Company");

        string[] categories = [.. rows.Select(r => r.Category)];
        var chart = new FxChart
        {
            Title = "Plan i wykonanie budżetu według kategorii",
            CategoryAxisTitle = "Kategoria kosztów",
            ValueAxisTitle = "Kwota (zł)",
            PlotAreaFill = "FFFFFF",
        };
        chart.Series.Add(new FxSeries("Plan (zł)", "'Budżet 2026'!$B$1", "'Budżet 2026'!$A$2:$A$5", "'Budżet 2026'!$B$2:$B$5", categories,
            [.. rows.Select(r => r.Plan)]) { Fill = SeriesDark });
        chart.Series.Add(new FxSeries("Wykonanie (zł)", "'Budżet 2026'!$C$1", "'Budżet 2026'!$A$2:$A$5", "'Budżet 2026'!$C$2:$C$5", categories,
            [.. rows.Select(r => r.Actual)]) { Fill = SeriesOrange, Pattern = "wdUpDiag", DataLabels = true });
        budget.Chart(chart, "Wykres budżetu", 3, "F7", "M22",
            "Wykres kolumnowy porównujący plan i wykonanie budżetu w czterech kategoriach. Największa pozycja to wynagrodzenia: plan 120 000 zł, wykonanie 118 500 zł; wykonanie przekracza plan tylko w kategorii Sprzęt.");

        var info = fx.AddSheet("Informacje");
        info.Columns(1, 1, 70);
        info.Text("A1", "Informacje o skoroszycie", heading);
        info.Text("A2", "Skoroszyt zawiera plan i wykonanie budżetu działu w roku 2026.");
        info.Text("A3", "Regulamin budżetowania (strona Example Company)", link);
        info.Hyperlink("A3", "https://example.com/regulamin-budzetowania");
        info.Text("A4", "Dane aktualizuje dział finansowy do piątego dnia każdego miesiąca.");
        info.Image("Ozdobnik", 2, "C1", "D3", description: "", decorative: true);
        return fx.Build();
    }
}
