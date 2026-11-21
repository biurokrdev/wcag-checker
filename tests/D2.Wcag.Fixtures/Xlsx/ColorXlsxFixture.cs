namespace D2.Wcag.Fixtures.Xlsx;

// color.xlsx – kolor i kontrast (grupa X3). Kontrasty policzone wzorem WCAG; pilnuje ich test XlsxFixtureModelTests.
//
// Właściwości: tytuł „Paleta kolorów – przypadki kontrastu”, język pl-PL.
//
// Arkusz 1 „Kontrast” – tabela „TabelaKontrast” A1:B19 (bez stylu tabeli), kolumna A opis (czarny), kolumna B próbka:
//   B2   czcionka RGB 999999 na białym, 11 pt → 2,8:1 (2,85) – BŁĄD kontrastu
//   B3   czcionka theme=4 (accent1 4472C4) tint≈0,4 → 8EA9DB na białym → ok. 2,4:1 – BŁĄD
//   B4   czcionka theme=1 (dk1 000000) na wypełnieniu theme=0 tint≈−0,05 (F2F2F2) → 18,7:1 – poprawne
//   B5   czcionka indexed=22 (C0C0C0) na białym → 1,8:1 – BŁĄD
//   B6   czcionka indexed=8 (000000) na wypełnieniu indexed=13 (FFFF00) → 19,5:1 – poprawne
//   B7   14 pt pogrubiony 949494 na białym → 3,03:1, duży tekst (próg 3:1) – poprawne (granica)
//   B8   14 pt pogrubiony 959595 na białym → 2,99:1 (dokładnie 2,995) – BŁĄD (tuż pod progiem 3:1; nie wolno pokazać „3,0:1”)
//   B9   14 pt NIEpogrubiony 949494 → 3,03:1 przy progu 4,5:1 – BŁĄD (to nie jest duży tekst)
//   B10  18 pt zwykły 949494 → 3,03:1, duży tekst – poprawne
//   B11  11 pt 767676 na białym → 4,54:1 – poprawne (granica 4,5:1)
//   B12  11 pt 777777 na białym → 4,47:1 (dokładnie 4,478) – BŁĄD (tuż pod progiem; nie wolno pokazać „4,5:1”)
//   B13  biała czcionka FFFFFF na wypełnieniu FFC000 → 1,6:1 – BŁĄD
//   B14  czarna czcionka na wzorze gray125 (czarny wzór na białym) → tło przybliżone DFDFDF (15,7:1), poprawne, flaga przybliżenia
//   B15  czcionka auto="1" bez wypełnienia → 21:1 – poprawne
//   B16  styl 999999 jak w B2, ale komórka PUSTA → reguła ma ją pominąć
//   B17  tekst sformatowany (teksty wspólne): „Termin: ” bez formatowania + „pilne” w kolorze FF0000 → fragment 4,0:1 – BŁĄD
//   B18  czcionka 1F3864 na wypełnieniu 203764 → ok. 1,0:1 – BŁĄD
//   B19  biała czcionka FFFFFF bez wypełnienia (biały na białym) → 1,0:1 – BŁĄD (treść niewidoczna)
// Arkusz 2 „Zakres szarości” – tabela „TabelaSzarosci” A1:C40 (bez stylu):
//   A1:C1 nagłówki czarne pogrubione;  A2:C39 liczby czcionką 999999 (ten sam styl co B2) → jedno zgłoszenie na zakres
//   'Zakres szarości'!A2:C39;  A40:C40 liczby czarne
// Arkusz 3 „Formatowanie warunkowe” – tabela „TabelaWyniki” A1:F6 (Pozycja | Saldo | Trend | Status | Ocena | Udział):
//   B2:B6  cellIs lessThan 0 → dxf 0: sama czerwona czcionka FF0000 (tylko kolor → XL-COLOR-ONLY; 4,0:1 → XL-CONTRAST-CF)
//   B2:B6  cellIs greaterThan 1000 → dxf 2: pogrubienie + czcionka 006100 na wypełnieniu C6EFCE (kontrast ok. 6,4:1; nie tylko kolor)
//   C2:C6  skala kolorów 3-kolorowa F8696B / FFEB84 / 63BE7B (tylko kolor)
//   D2:D6  containsText „błąd” → dxf 1: biała czcionka FFFFFF na żółtym FFFF00 (1,07:1 → XL-CONTRAST-CF)
//   E2:E6  zestaw ikon 3Arrows (kształt strzałek – nie tylko kolor)
//   F2:F6  pasek danych 638EC6
// Arkusz 4 „Wykresy kolorów” – tabela „TabelaSerie” A1:D5 (Miesiąc | Produkt A | Produkt B | Produkt C); wszystkie wykresy
// mają tytuł, tytuły osi i opis alternatywny (żeby nie prowokować reguł X2), białe tło obszaru kreślenia:
//   id 2  „Wykres 1” kolumnowy, F1:M15: serie pełnym kolorem FF0000 / 00B050 / FFFF00, bez wzorów i etykiet →
//         XL-CHART-COLOR (tylko kolor; czerwony–zielony; żółty do białego tła 1,07:1)
//   id 3  „Wykres 2” liniowy, F17:M31: linie FF0000 / 00B050 / 0070C0, bez znaczników (none) i ta sama kreska → XL-CHART-COLOR
//   id 4  „Wykres 3” liniowy, F33:M47: linie 1F3864 / D2691E / 000000 ze znacznikami circle / square / triangle
//         i kreskami solid / dash / sysDot → poprawny (serie różnią się nie tylko kolorem)
public static class ColorXlsxFixture
{
    public static byte[] Build()
    {
        var fx = new FixtureXlsx { Title = "Paleta kolorów – przypadki kontrastu", Language = "pl-PL" };
        var s = fx.Styles;
        var bold = s.Xf(font: s.Font(bold: true));
        var gray = s.Xf(font: s.Font("rgb=\"FF999999\""));

        var contrast = fx.AddSheet("Kontrast");
        contrast.Columns(1, 2, 48);
        contrast.Row("A1", bold, "Przypadek", "Próbka");
        (string Label, string? Sample, int Style)[] cases =
        [
            ("Szary RGB 999999 na białym", "Szary tekst na białym tle", gray),
            ("Kolor motywu accent1 z rozjaśnieniem 40%", "Tekst w kolorze motywu z tint", s.Xf(font: s.Font("theme=\"4\" tint=\"0.39997558519241921\""))),
            ("Kolor motywu dk1 na tle lt1 przyciemnionym o 5%", "Czarny motywu na jasnoszarym motywu",
                s.Xf(font: s.Font("theme=\"1\""), fill: s.Fill("theme=\"0\" tint=\"-4.9989318521683403E-2\""))),
            ("Kolor indeksowany 22 na białym", "Srebrny z palety indeksowanej", s.Xf(font: s.Font("indexed=\"22\""))),
            ("Kolor indeksowany 8 na tle indeksowanym 13", "Czarny na żółtym z palety", s.Xf(font: s.Font("indexed=\"8\""), fill: s.Fill("indexed=\"13\""))),
            ("14 pt pogrubiony 949494 – granica 3:1", "Duży tekst na granicy", s.Xf(font: s.Font("rgb=\"FF949494\"", size: 14, bold: true))),
            ("14 pt pogrubiony 959595 – tuż pod 3:1", "Duży tekst pod progiem", s.Xf(font: s.Font("rgb=\"FF959595\"", size: 14, bold: true))),
            ("14 pt zwykły 949494 – to nie jest duży tekst", "Tekst 14 pt bez pogrubienia", s.Xf(font: s.Font("rgb=\"FF949494\"", size: 14))),
            ("18 pt zwykły 949494 – duży tekst", "Tekst 18 pt", s.Xf(font: s.Font("rgb=\"FF949494\"", size: 18))),
            ("11 pt 767676 – granica 4,5:1", "Tekst na granicy 4,5:1", s.Xf(font: s.Font("rgb=\"FF767676\""))),
            ("11 pt 777777 – tuż pod 4,5:1", "Tekst tuż pod progiem", s.Xf(font: s.Font("rgb=\"FF777777\""))),
            ("Biały na pomarańczowym FFC000", "Biały tekst na jasnym wypełnieniu", s.Xf(font: s.Font("rgb=\"FFFFFFFF\""), fill: s.Fill("rgb=\"FFFFC000\""))),
            ("Czarny na wzorze gray125", "Tekst na tle ze wzorem", s.Xf(font: s.Font("rgb=\"FF000000\""), fill: s.Fill("rgb=\"FF000000\"", "gray125", "rgb=\"FFFFFFFF\""))),
            ("Kolor automatyczny", "Tekst w kolorze automatycznym", s.Xf(font: s.Font("auto=\"1\""))),
            ("Pusta komórka ze stylem szarym", null, gray),
            ("Tekst sformatowany z czerwonym fragmentem", "", 0),
            ("Granatowy na granatowym", "Ciemny tekst na ciemnym tle", s.Xf(font: s.Font("rgb=\"FF1F3864\""), fill: s.Fill("rgb=\"FF203764\""))),
            ("Biały tekst na białym tle", "Tekst niewidoczny", s.Xf(font: s.Font("rgb=\"FFFFFFFF\""))),
        ];
        for (var i = 0; i < cases.Length; i++)
        {
            var row = i + 2;
            contrast.Text($"A{row}", cases[i].Label);
            if (cases[i].Sample is null)
            {
                contrast.Styled($"B{row}", cases[i].Style);
            }
            else if (cases[i].Sample!.Length == 0)
            {
                contrast.Rich($"B{row}", "<r><t xml:space=\"preserve\">Termin: </t></r><r><rPr><sz val=\"11\"/><color rgb=\"FFFF0000\"/><rFont val=\"Calibri\"/><family val=\"2\"/></rPr><t>pilne</t></r>");
            }
            else
            {
                contrast.Text($"B{row}", cases[i].Sample!, cases[i].Style);
            }
        }

        contrast.Table(new FxTable("TabelaKontrast", "A1:B19", ["Przypadek", "Próbka"]) { Style = null });

        var range = fx.AddSheet("Zakres szarości");
        range.Row("A1", bold, "Seria A", "Seria B", "Seria C");
        for (var row = 2; row <= 40; row++)
        {
            var style = row <= 39 ? gray : 0;
            range.Number($"A{row}", row * 3, style).Number($"B{row}", row * 5, style).Number($"C{row}", row * 7, style);
        }

        range.Table(new FxTable("TabelaSzarosci", "A1:C40", ["Seria A", "Seria B", "Seria C"]) { Style = null });

        var conditional = fx.AddSheet("Formatowanie warunkowe");
        conditional.Row("A1", bold, "Pozycja", "Saldo", "Trend", "Status", "Ocena", "Udział");
        (string Item, double Balance, double Trend, string Status, double Score, double Share)[] data =
        [
            ("Kasa", 1500, 3, "ok", 5, 0.4),
            ("Bank", -200, -1, "błąd księgowania", 2, 0.1),
            ("Lokata", 800, 2, "ok", 4, 0.2),
            ("Kredyt", -950, -4, "błąd salda", 1, 0.05),
            ("Rezerwa", 300, 1, "ok", 3, 0.25),
        ];
        for (var i = 0; i < data.Length; i++)
        {
            var row = i + 2;
            conditional.Text($"A{row}", data[i].Item).Number($"B{row}", data[i].Balance).Number($"C{row}", data[i].Trend)
                .Text($"D{row}", data[i].Status).Number($"E{row}", data[i].Score).Number($"F{row}", data[i].Share);
        }

        conditional.Table(new FxTable("TabelaWyniki", "A1:F6", ["Pozycja", "Saldo", "Trend", "Status", "Ocena", "Udział"]) { Style = null });
        var redOnly = s.Dxf("<font><color rgb=\"FFFF0000\"/></font>");
        var whiteOnYellow = s.Dxf("<font><color rgb=\"FFFFFFFF\"/></font><fill><patternFill><bgColor rgb=\"FFFFFF00\"/></patternFill></fill>");
        var goodGreen = s.Dxf("<font><b/><color rgb=\"FF006100\"/></font><fill><patternFill><bgColor rgb=\"FFC6EFCE\"/></patternFill></fill>");
        conditional.ConditionalFormats.Add(
            $"<conditionalFormatting sqref=\"B2:B6\"><cfRule type=\"cellIs\" dxfId=\"{redOnly}\" priority=\"1\" operator=\"lessThan\"><formula>0</formula></cfRule>"
            + $"<cfRule type=\"cellIs\" dxfId=\"{goodGreen}\" priority=\"2\" operator=\"greaterThan\"><formula>1000</formula></cfRule></conditionalFormatting>");
        conditional.ConditionalFormats.Add(
            "<conditionalFormatting sqref=\"C2:C6\"><cfRule type=\"colorScale\" priority=\"3\"><colorScale><cfvo type=\"min\"/><cfvo type=\"percentile\" val=\"50\"/><cfvo type=\"max\"/>"
            + "<color rgb=\"FFF8696B\"/><color rgb=\"FFFFEB84\"/><color rgb=\"FF63BE7B\"/></colorScale></cfRule></conditionalFormatting>");
        conditional.ConditionalFormats.Add(
            $"<conditionalFormatting sqref=\"D2:D6\"><cfRule type=\"containsText\" dxfId=\"{whiteOnYellow}\" priority=\"4\" operator=\"containsText\" text=\"błąd\">"
            + "<formula>NOT(ISERROR(SEARCH(\"błąd\",D2)))</formula></cfRule></conditionalFormatting>");
        conditional.ConditionalFormats.Add(
            "<conditionalFormatting sqref=\"E2:E6\"><cfRule type=\"iconSet\" priority=\"5\"><iconSet iconSet=\"3Arrows\"><cfvo type=\"percent\" val=\"0\"/><cfvo type=\"percent\" val=\"33\"/><cfvo type=\"percent\" val=\"67\"/></iconSet></cfRule></conditionalFormatting>");
        conditional.ConditionalFormats.Add(
            "<conditionalFormatting sqref=\"F2:F6\"><cfRule type=\"dataBar\" priority=\"6\"><dataBar><cfvo type=\"min\"/><cfvo type=\"max\"/><color rgb=\"FF638EC6\"/></dataBar></cfRule></conditionalFormatting>");

        var charts = fx.AddSheet("Wykresy kolorów");
        charts.Row("A1", bold, "Miesiąc", "Produkt A", "Produkt B", "Produkt C");
        string[] monthNames = ["Styczeń", "Luty", "Marzec", "Kwiecień"];
        double[][] values = [[12, 15, 14, 18], [9, 11, 13, 12], [5, 7, 6, 9]];
        for (var i = 0; i < monthNames.Length; i++)
        {
            charts.Text($"A{i + 2}", monthNames[i]).Number($"B{i + 2}", values[0][i]).Number($"C{i + 2}", values[1][i]).Number($"D{i + 2}", values[2][i]);
        }

        charts.Table(new FxTable("TabelaSerie", "A1:D5", ["Miesiąc", "Produkt A", "Produkt B", "Produkt C"]) { Style = null });

        FxSeries Series(int index) => new($"Produkt {(char)('A' + index)}", $"'Wykresy kolorów'!${(char)('B' + index)}$1",
            "'Wykresy kolorów'!$A$2:$A$5", $"'Wykresy kolorów'!${(char)('B' + index)}$2:${(char)('B' + index)}$5", monthNames, values[index]);

        FxChart Chart(string type, string title) => new()
        {
            Type = type,
            Title = title,
            CategoryAxisTitle = "Miesiąc",
            ValueAxisTitle = "Sprzedaż (szt.)",
            PlotAreaFill = "FFFFFF",
        };

        var bars = Chart("bar", "Sprzedaż produktów – kolumny");
        bars.Series.Add(Series(0) with { Fill = "FF0000" });
        bars.Series.Add(Series(1) with { Fill = "00B050" });
        bars.Series.Add(Series(2) with { Fill = "FFFF00" });
        charts.Chart(bars, "Wykres 1", 2, "F1", "M15", "Wykres kolumnowy sprzedaży trzech produktów w czterech miesiącach; najwyższa sprzedaż: Produkt A w kwietniu (18 szt.).");

        var lines = Chart("line", "Sprzedaż produktów – linie");
        lines.Series.Add(Series(0) with { Line = "FF0000" });
        lines.Series.Add(Series(1) with { Line = "00B050" });
        lines.Series.Add(Series(2) with { Line = "0070C0" });
        charts.Chart(lines, "Wykres 2", 3, "F17", "M31", "Wykres liniowy sprzedaży trzech produktów w czterech miesiącach; wszystkie serie rosną od stycznia do kwietnia.");

        var distinct = Chart("line", "Sprzedaż produktów – linie rozróżnialne");
        distinct.Series.Add(Series(0) with { Line = "1F3864", Marker = "circle", Dash = "solid" });
        distinct.Series.Add(Series(1) with { Line = "D2691E", Marker = "square", Dash = "dash" });
        distinct.Series.Add(Series(2) with { Line = "000000", Marker = "triangle", Dash = "sysDot" });
        charts.Chart(distinct, "Wykres 3", 4, "F33", "M47", "Wykres liniowy sprzedaży trzech produktów; serie mają różne znaczniki i style linii.");
        return fx.Build();
    }
}
