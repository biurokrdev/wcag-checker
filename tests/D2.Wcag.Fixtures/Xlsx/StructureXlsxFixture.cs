namespace D2.Wcag.Fixtures.Xlsx;

// structure.xlsx – problemy struktury, nawigacji i metadanych (grupa X1).
//
// Właściwości: BRAK tytułu (XL-TITLE), BRAK języka (XL-LANG), autor Example Company S. A.
// Nazwa zdefiniowana: „Zakres_Sprzedaz” = Arkusz1!$A$1:$C$5.
//
// Arkusz 1 „Arkusz1” (nazwa domyślna PL → XL-SHEET-NAME):
//   A1:C5   blok danych z pogrubionym wierszem nagłówka (Miesiąc | Sprzedaż | Koszt), NIE jest Tabelą Excela → XL-TABLE-MISSING
//   A7      tekst „Wartości zaznaczone na czerwono wymagają korekty, a podsumowanie znajduje się po prawej.” → XL-SENSORY
// Arkusz 2 „Sheet2” (nazwa domyślna EN → XL-SHEET-NAME):
//   A1:D1   scalony tytuł „Zestawienie kwartalne” nad danymi (wariant „tytuł nad tabelą” reguły XL-MERGED – bez zgłoszenia)
//   A2:D2   pogrubione nagłówki Region | Q1 | Q2 | Uwagi;  A3:D5 dane
//   B4:C4   komórki scalone W DANYCH → XL-MERGED
//   wiersz 6 pusty, A7:D8 dalszy ciąg tych samych danych → XL-BLANK-LAYOUT (pusty wiersz dzieli blok); dane bez tabeli
// Arkusz 3 „Dane ukryte” (stan hidden), A1:B3 dane → XL-HIDDEN (ukryty arkusz z danymi)
// Arkusz 4 „Słownik” (stan veryHidden), A1:A3 dane → XL-HIDDEN
// Arkusz 5 „Koszty”:
//   A1:E6   tabela „TabelaKoszty” z nagłówkami Pozycja | Netto | VAT | Brutto | Uwagi
//   wiersz 4 ukryty (z danymi) → XL-HIDDEN;  kolumna C ukryta (z danymi) → XL-HIDDEN
//   E3      tekst „wartość ukryta formatem” z formatem liczbowym „;;;” → XL-HIDDEN
// Arkusz 6 „Tabele”:
//   A1:C4   tabela „Tabela1” BEZ wiersza nagłówka (headerRowCount="0") → XL-TABLE-HEADER
//   E1:G4   tabela „Tabela2” z nagłówkami domyślnymi Kolumna1 | Kolumna2 | Kolumna3 → XL-TABLE-HEADER-NAME
// Arkusz 7 „Późny start”:
//   H20:I22 tabela „TabelaPozna” (Kod | Opis); nic przed H20 → XL-START-A1 (dobra praktyka)
// Arkusz 8 „Q” (nazwa jednoznakowa → XL-SHEET-NAME, wariant heurystyczny): A1 „Notatka robocza”
public static class StructureXlsxFixture
{
    public static byte[] Build()
    {
        var fx = new FixtureXlsx();
        var bold = fx.Styles.Xf(font: fx.Styles.Font(bold: true));
        var invisible = fx.Styles.Xf(numFmtCode: ";;;");
        fx.DefinedNames.Add(("Zakres_Sprzedaz", "Arkusz1!$A$1:$C$5", null, false));

        var first = fx.AddSheet("Arkusz1");
        first.Row("A1", bold, "Miesiąc", "Sprzedaż", "Koszt");
        (string Month, double Sales, double Cost)[] months = [("Styczeń", 1200, 800), ("Luty", 1350, 820), ("Marzec", 990, 760), ("Kwiecień", 1420, 900)];
        for (var i = 0; i < months.Length; i++)
        {
            first.Text($"A{i + 2}", months[i].Month).Number($"B{i + 2}", months[i].Sales).Number($"C{i + 2}", months[i].Cost);
        }

        first.Text("A7", "Wartości zaznaczone na czerwono wymagają korekty, a podsumowanie znajduje się po prawej.");

        var second = fx.AddSheet("Sheet2");
        second.Text("A1", "Zestawienie kwartalne", bold).Merge("A1:D1");
        second.Row("A2", bold, "Region", "Q1", "Q2", "Uwagi");
        second.Text("A3", "Północ").Number("B3", 410).Number("C3", 455).Text("D3", "wzrost");
        second.Text("A4", "Południe").Text("B4", "brak danych za półrocze").Merge("B4:C4").Text("D4", "do uzupełnienia");
        second.Text("A5", "Wschód").Number("B5", 300).Number("C5", 310).Text("D5", "stabilnie");
        second.Text("A7", "Zachód").Number("B7", 520).Number("C7", 498).Text("D7", "spadek");
        second.Text("A8", "Centrum").Number("B8", 610).Number("C8", 640).Text("D8", "wzrost");

        var hidden = fx.AddSheet("Dane ukryte", "hidden");
        hidden.Row("A1", 0, "Klient", "Rabat");
        hidden.Text("A2", "Alfa sp. z o.o.").Number("B2", 0.15).Text("A3", "Beta S.A.").Number("B3", 0.2);

        var veryHidden = fx.AddSheet("Słownik", "veryHidden");
        veryHidden.Text("A1", "Status").Text("A2", "Otwarte").Text("A3", "Zamknięte");

        var costs = fx.AddSheet("Koszty");
        costs.Row("A1", 0, "Pozycja", "Netto", "VAT", "Brutto", "Uwagi");
        (string Item, double Net, string Note)[] items =
            [("Licencje", 1000, "roczne"), ("Serwis", 400, "kwartalny"), ("Hosting", 250, "miesięczny"), ("Domeny", 80, "roczne"), ("Wsparcie", 600, "umowa")];
        for (var i = 0; i < items.Length; i++)
        {
            var row = i + 2;
            costs.Text($"A{row}", items[i].Item).Number($"B{row}", items[i].Net).Number($"C{row}", items[i].Net * 0.23)
                .Number($"D{row}", items[i].Net * 1.23).Text($"E{row}", items[i].Note);
        }

        costs.Text("E3", "wartość ukryta formatem", invisible);
        costs.HideRow(4);
        costs.Columns(3, 3, 12, hidden: true);
        costs.Table(new FxTable("TabelaKoszty", "A1:E6", ["Pozycja", "Netto", "VAT", "Brutto", "Uwagi"]));

        var tables = fx.AddSheet("Tabele");
        for (var row = 1; row <= 4; row++)
        {
            tables.Text($"A{row}", $"Oddział {row}").Number($"B{row}", row * 10).Number($"C{row}", row * 12);
        }

        tables.Table(new FxTable("Tabela1", "A1:C4", ["Kolumna1", "Kolumna2", "Kolumna3"]) { HeaderRow = false });
        tables.Row("E1", 0, "Kolumna1", "Kolumna2", "Kolumna3");
        for (var row = 2; row <= 4; row++)
        {
            tables.Text($"E{row}", $"Produkt {row - 1}").Number($"F{row}", row * 5).Number($"G{row}", row * 7);
        }

        tables.Table(new FxTable("Tabela2", "E1:G4", ["Kolumna1", "Kolumna2", "Kolumna3"]));

        var late = fx.AddSheet("Późny start");
        late.Row("H20", 0, "Kod", "Opis");
        late.Text("H21", "A-01").Text("I21", "Pozycja pierwsza").Text("H22", "A-02").Text("I22", "Pozycja druga");
        late.Table(new FxTable("TabelaPozna", "H20:I22", ["Kod", "Opis"]));

        fx.AddSheet("Q").Text("A1", "Notatka robocza");
        return fx.Build();
    }
}
