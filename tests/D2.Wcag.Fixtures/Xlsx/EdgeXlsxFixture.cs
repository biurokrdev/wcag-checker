using System.Globalization;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Fixtures.Xlsx;

// edge.xlsx – przypadki brzegowe parsera; plik w odmianie STRICT Open XML (Build(strict: false) daje tę samą treść
// w odmianie Transitional – test porównuje oba modele).
//
// Właściwości: tytuł „Przypadki brzegowe – zażółć gęślą jaźń”, język pl-PL, temat, słowa kluczowe, opis.
// Nazwy zdefiniowane: „Zakres_Duży” = 'Duży'!$A$1:$C$200000; „Lokalna” (lokalna dla arkusza 2) = 'Q1''25'!$A$1;
//   „_xlnm._FilterDatabase” (ukryta, lokalna dla arkusza 4).
// Style: własna paleta indexedColors (domyślne 64 kolory, indeks 10 zmieniony z FF0000 na 006100).
//
// Arkusz 1 „Zażółć gęślą jaźń” (polskie znaki w nazwie):
//   A1  tekst wspólny „Zażółć gęślą jaźń – ŻÓŁW”;  notatka (autor Jan Kowalski): „Notatka: sprawdzić źródło danych.”
//   B1  tekst wbudowany (inlineStr) „Tekst wbudowany”
//   C1  liczba 42;  komentarz wątkowy (Anna Nowak): „Czy ta wartość jest aktualna?” + zaślepka „tc=…” w notatkach
//   D1  wartość logiczna (t="b", 1);  E1  błąd (t="e", #DIV/0!)
//   F1  formuła C1*2 z wartością z pamięci podręcznej 84
//   G1  formuła tekstowa (t="str") z wartością „Suma: 42”;  H1  formuła NOW() BEZ wartości (typ Formula)
//   A2  tekst „ukryta treść” z formatem liczbowym „;;;”
//   A3  tekst sformatowany (teksty wspólne): „Zwykły ” + „czerwony pogrubiony” (b, 14 pt, FF0000)
//   B3  tekst sformatowany wbudowany: „Motyw: ” + „akcent” (theme=4)
//   A4  tekst ze spacjami na brzegach „  spacje na brzegach  ”;  B4  znaki specjalne: znaki <&> "cudzysłów"
//   C4  liczba 45000 w formacie daty (numFmtId 14)
//   A5  tekst czcionką indexed=10 – kolor z własnej palety (006100)
//   A6  komórka z samym stylem (bez wartości)
// Arkusz 2 „Q1'25” (apostrof w nazwie): A1 tekst
// Arkusz 3 „Wyniki 📊” (emoji w nazwie): A1 tekst z emoji
// Arkusz 4 „Duży”: A1:C1 nagłówki (Lp | Kategoria | Wartość), wiersze 2–200000: liczba, tekst wspólny (4 kategorie), liczba
//   – razem 200 000 wierszy × 3 kolumny = 600 000 komórek
// Arkusz 5 „Pusty”: bez komórek
// Arkusz 6 „Wykres arkusz” – arkusz wykresu (chartsheet) z wykresem kolumnowym: tytuł, tytuły osi, opis alternatywny
public static class EdgeXlsxFixture
{
    public const int BigRows = 200_000;
    public const string PersonId = "{11111111-2222-3333-4444-555555555555}";
    public const string ThreadId = "{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}";

    public static byte[] Build() => Build(strict: true);

    public static byte[] Build(bool strict)
    {
        var fx = new FixtureXlsx
        {
            Strict = strict,
            Title = "Przypadki brzegowe – zażółć gęślą jaźń",
            Language = "pl-PL",
            Subject = "Fixtura testowa parsera",
            Keywords = "test; parser; XLSX",
            Description = "Skoroszyt z przypadkami brzegowymi do testów parsera.",
        };
        var s = fx.Styles;
        for (var i = 0; i < 64; i++)
        {
            s.IndexedColors.Add("00" + (i == 10 ? "006100" : XlsxColors.DefaultIndexedPalette[i]));
        }

        var invisible = s.Xf(numFmtCode: ";;;");
        var date = s.Xf(numFmtId: 14);
        var palette = s.Xf(font: s.Font("indexed=\"10\""));
        var bold = s.Xf(font: s.Font(bold: true));
        fx.Persons.Add((PersonId, "Anna Nowak"));
        fx.DefinedNames.Add(("Zakres_Duży", $"'Duży'!$A$1:$C${BigRows}", null, false));
        fx.DefinedNames.Add(("Lokalna", "'Q1''25'!$A$1", 1, false));
        fx.DefinedNames.Add(("_xlnm._FilterDatabase", $"'Duży'!$A$1:$C${BigRows}", 3, true));

        var first = fx.AddSheet("Zażółć gęślą jaźń");
        first.Text("A1", "Zażółć gęślą jaźń – ŻÓŁW").Inline("B1", "Tekst wbudowany").Number("C1", 42).Bool("D1", true).Error("E1", "#DIV/0!");
        first.Formula("F1", "C1*2", "84").Formula("G1", "\"Suma: \"&C1", "Suma: 42", "str").Formula("H1", "NOW()", null);
        first.Text("A2", "ukryta treść", invisible);
        first.Rich("A3", "<r><t xml:space=\"preserve\">Zwykły </t></r><r><rPr><b/><sz val=\"14\"/><color rgb=\"FFFF0000\"/><rFont val=\"Calibri\"/><family val=\"2\"/></rPr><t>czerwony pogrubiony</t></r>");
        first.Inline("B3", "", isInnerXml: "<r><t xml:space=\"preserve\">Motyw: </t></r><r><rPr><sz val=\"11\"/><color theme=\"4\"/><rFont val=\"Calibri\"/><family val=\"2\"/></rPr><t>akcent</t></r>");
        first.Text("A4", "  spacje na brzegach  ").Text("B4", "znaki <&> \"cudzysłów\"").Number("C4", 45000, date);
        first.Text("A5", "Kolor z własnej palety", palette);
        first.Styled("A6", bold);
        first.Comment("A1", "Jan Kowalski", "Notatka: sprawdzić źródło danych.");
        first.ThreadedComment("C1", PersonId, ThreadId, "Czy ta wartość jest aktualna?");

        fx.AddSheet("Q1'25").Text("A1", "Kwartał pierwszy 2025");
        fx.AddSheet("Wyniki 📊").Text("A1", "Wyniki z emoji w nazwie arkusza 📊");

        var big = fx.AddSheet("Duży");
        big.Row("A1", bold, "Lp", "Kategoria", "Wartość");
        int[] categories =
        [
            fx.SharedStringIndex("Przychody"), fx.SharedStringIndex("Koszty stałe"), fx.SharedStringIndex("Koszty zmienne"), fx.SharedStringIndex("Inwestycje"),
        ];
        big.BulkRows = xml =>
        {
            for (var row = 2; row <= BigRows; row++)
            {
                // Wartości pseudolosowe, ale deterministyczne (kongruencja liniowa) – dane nie kompresują się podejrzanie dobrze.
                var value = unchecked((uint)row * 2654435761u) % 10000 / 100.0;
                xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{row}\"><c r=\"A{row}\"><v>{row - 1}</v></c><c r=\"B{row}\" t=\"s\"><v>{categories[unchecked((uint)row * 2246822519u) % 4]}</v></c><c r=\"C{row}\"><v>{value.ToString("R", CultureInfo.InvariantCulture)}</v></c></row>");
            }
        };

        fx.AddSheet("Pusty");

        var chart = new FxChart
        {
            Title = "Pierwsze wartości dużego arkusza",
            CategoryAxisTitle = "Kategoria",
            ValueAxisTitle = "Wartość",
            PlotAreaFill = "FFFFFF",
        };
        chart.Series.Add(new FxSeries("Wartość", "'Duży'!$C$1", "'Duży'!$B$2:$B$4", "'Duży'!$C$2:$C$4", ["Pierwsza", "Druga", "Trzecia"], [10, 20, 30])
        {
            Fill = "1F3864",
            DataLabels = true,
        });
        fx.AddChartSheet("Wykres arkusz", chart, "Wykres 1", "Wykres kolumnowy trzech pierwszych wartości z arkusza Duży.");
        return fx.Build();
    }
}
