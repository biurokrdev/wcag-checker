namespace D2.Wcag.Fixtures.Xlsx;

// nontext.xlsx – treści nietekstowe, łącza i kontrolki (grupa X2).
//
// Właściwości: tytuł „Raport sprzedaży – obiekty i łącza”, język pl-PL.
//
// Arkusz 1 „Obrazy i obiekty” (A1: nagłówek tekstowy); obiekty rysunku w kolejności z pliku:
//   id 2  obraz „Obraz 1”, B3:C6, BEZ descr i bez oznaczenia dekoracyjności → XL-ALT-MISSING (+ XL-TEXT-IN-IMAGE)
//   id 3  obraz „Obraz 2”, B8:C11, dekoracyjny (adec:decorative, descr="") → poprawny, bez zgłoszeń
//   id 4  obraz „Obraz 3”, B13:C16, descr „Obraz 1” → XL-ALT-POOR (opis domyślny)
//   id 5  obraz „Obraz 4”, B18:C21, plik logo.png, descr „logo.png” → XL-ALT-POOR (opis = nazwa pliku)
//   id 6  obraz „Obraz 5”, E3:F6, dobry opis „Schemat obiegu faktury: rejestracja, akceptacja, księgowanie”,
//         z łączem (hlinkClick → https://example.com/proces) → poprawny (tylko XL-TEXT-IN-IMAGE)
//   id 7  pole tekstowe „pole tekstowe 6” (txBox), E8:H11, tekst „Uwaga: dane za grudzień są szacunkowe i zostaną
//         skorygowane w styczniu.” → XL-TEXTBOX
//   id 8  kształt „Prostokąt 7”, E13:F16, bez tekstu i bez descr → XL-ALT-MISSING (kształt)
//   id 9  grupa „Grupa 8”, E18:H21, descr „Ikony statusu: zielona – zakończone, żółta – w toku”; w grupie
//   id 10 obraz „Obraz 9” i id 11 obraz „Obraz 10” – oba bez descr (ParentGroupId = 9; opis ma grupa)
//   id 12 obraz „Obraz 11”, B23:C26, ukryty (hidden="1"), bez descr
// Arkusz 2 „Wykresy”:
//   A1:C5   tabela „TabelaSprzedaz” (Kwartał | Sprzedaż 2025 | Sprzedaż 2026)
//   id 2  wykres „Wykres 1”, E1:L15: BEZ tytułu (autoTitleDeleted=1), BEZ tytułów osi, BEZ opisu → XL-CHART
//   id 3  wykres „Wykres 2”, E17:L31: tytuł „Sprzedaż kwartalna”, opis alternatywny, ale BEZ tytułów osi → XL-CHART (osie)
//   (serie obu wykresów w kolorach automatycznych – bez jawnych wypełnień)
// Arkusz 3 „Łącza” (A1: nagłówek):
//   A2  tekst = surowy URL „https://example.com/raporty/2026/q1” → XL-LINK-TEXT
//   A3  „kliknij tutaj” → https://example.com/regulamin → XL-LINK-TEXT
//   A4  „tutaj”, łącze wewnętrzne do 'Wykresy'!A1 → XL-LINK-TEXT
//   A5  „Raport roczny 2025 (PDF, 2 MB)” → https://example.com/raport-2025.pdf → poprawne
//   A6  łącze na pustej komórce (brak tekstu) → https://example.com/pusty → XL-LINK-TEXT (pusty)
//   A7  „link” z etykietką ekranową „Strona główna Example Company” → https://example.com/ → XL-LINK-TEXT
// Arkusz 4 „Kontrolki” (A1: nagłówek):
//   „Check Box 1” (pole wyboru, B3:D4): bez etykiety i bez tekstu alternatywnego → XL-CONTROL
//   „Button 2” (przycisk, B6:D7): etykieta „Zapisz”, tekst alternatywny „Zapisz formularz” → poprawna
//   „Drop Down 3” (lista rozwijana, B9:D10): bez etykiety i bez tekstu alternatywnego, połączona z $F$9 → XL-CONTROL
//   „CommandButton1” (ActiveX, B12:D13) → XL-CONTROL (zawsze kontrola ręczna)
public static class NonTextXlsxFixture
{
    public static byte[] Build()
    {
        var fx = new FixtureXlsx { Title = "Raport sprzedaży – obiekty i łącza", Language = "pl-PL" };
        var bold = fx.Styles.Xf(font: fx.Styles.Font(bold: true));
        var link = fx.Styles.Xf(font: fx.Styles.Font("rgb=\"FF0563C1\"", underline: true));

        var objects = fx.AddSheet("Obrazy i obiekty");
        objects.Text("A1", "Przykłady obiektów graficznych", bold);
        objects.Image("Obraz 1", 2, "B3", "C6");
        objects.Image("Obraz 2", 3, "B8", "C11", description: "", decorative: true);
        objects.Image("Obraz 3", 4, "B13", "C16", description: "Obraz 1");
        objects.Image("Obraz 4", 5, "B18", "C21", description: "logo.png", file: "logo.png");
        objects.Image("Obraz 5", 6, "E3", "F6", description: "Schemat obiegu faktury: rejestracja, akceptacja, księgowanie",
            hyperlink: "https://example.com/proces");
        objects.Shape("pole tekstowe 6", 7, "E8", "H11", "Uwaga: dane za grudzień są szacunkowe i zostaną skorygowane w styczniu.", textBox: true);
        objects.Shape("Prostokąt 7", 8, "E13", "F16");
        objects.Group("Grupa 8", 9, "E18", "H21", "Ikony statusu: zielona – zakończone, żółta – w toku",
            FxDrawingItem.Picture("Obraz 9", 10), FxDrawingItem.Picture("Obraz 10", 11));
        objects.Image("Obraz 11", 12, "B23", "C26", hidden: true);

        var charts = fx.AddSheet("Wykresy");
        charts.Row("A1", 0, "Kwartał", "Sprzedaż 2025", "Sprzedaż 2026");
        string[] quarters = ["I kwartał", "II kwartał", "III kwartał", "IV kwartał"];
        double[] sales2025 = [210, 245, 230, 290];
        double[] sales2026 = [250, 260, 275, 310];
        for (var i = 0; i < quarters.Length; i++)
        {
            charts.Text($"A{i + 2}", quarters[i]).Number($"B{i + 2}", sales2025[i]).Number($"C{i + 2}", sales2026[i]);
        }

        charts.Table(new FxTable("TabelaSprzedaz", "A1:C5", ["Kwartał", "Sprzedaż 2025", "Sprzedaż 2026"]));

        FxChart Chart(string? title, bool deleted)
        {
            var chart = new FxChart { Title = title, AutoTitleDeleted = deleted };
            chart.Series.Add(new FxSeries("Sprzedaż 2025", "Wykresy!$B$1", "Wykresy!$A$2:$A$5", "Wykresy!$B$2:$B$5", quarters, sales2025));
            chart.Series.Add(new FxSeries("Sprzedaż 2026", "Wykresy!$C$1", "Wykresy!$A$2:$A$5", "Wykresy!$C$2:$C$5", quarters, sales2026));
            return chart;
        }

        charts.Chart(Chart(null, true), "Wykres 1", 2, "E1", "L15");
        charts.Chart(Chart("Sprzedaż kwartalna", false), "Wykres 2", 3, "E17", "L31",
            "Wykres kolumnowy sprzedaży kwartalnej w latach 2025 i 2026; w każdym kwartale 2026 sprzedaż jest wyższa niż rok wcześniej.");

        var links = fx.AddSheet("Łącza");
        links.Columns(1, 1, 45);
        links.Text("A1", "Materiały źródłowe", bold);
        links.Text("A2", "https://example.com/raporty/2026/q1", link).Hyperlink("A2", "https://example.com/raporty/2026/q1");
        links.Text("A3", "kliknij tutaj", link).Hyperlink("A3", "https://example.com/regulamin");
        links.Text("A4", "tutaj", link).Hyperlink("A4", location: "'Wykresy'!A1", display: "tutaj");
        links.Text("A5", "Raport roczny 2025 (PDF, 2 MB)", link).Hyperlink("A5", "https://example.com/raport-2025.pdf");
        links.Hyperlink("A6", "https://example.com/pusty");
        links.Text("A7", "link", link).Hyperlink("A7", "https://example.com/", tooltip: "Strona główna Example Company");

        var controls = fx.AddSheet("Kontrolki");
        controls.Text("A1", "Formularz z kontrolkami", bold);
        controls.FormControl("Check Box 1", 1025, "CheckBox", "B3", "D4");
        controls.FormControl("Button 2", 1026, "Button", "B6", "D7", altText: "Zapisz formularz", label: "Zapisz");
        controls.FormControl("Drop Down 3", 1027, "Drop", "B9", "D10", linkedCell: "$F$9");
        controls.ActiveX("CommandButton1", 1028, "B12", "D13");
        return fx.Build();
    }
}
