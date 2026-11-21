namespace D2.Wcag.Fixtures.Xlsx;

// forms.xlsx – arkusz-formularz: walidacja danych i komórki do wypełnienia (grupa X2).
//
// Właściwości: tytuł „Formularz zgłoszenia urlopu”, język pl-PL.
//
// Arkusz 1 „Formularz zgłoszenia” – arkusz CHRONIONY (sheetProtection); komórki do wypełnienia są odblokowane
// (protection locked="0") i mają jasnożółte wypełnienie FFF2CC:
//   A1       nagłówek „Formularz zgłoszenia urlopu” (14 pt, pogrubiony)
//   A3 | B3  etykieta „Imię i nazwisko” | pole bez walidacji → poprawne (etykieta po lewej)
//   A4 | B4  etykieta „Zgoda przełożonego” | walidacja lista „Tak,Nie” BEZ komunikatu wejściowego i BEZ komunikatu o błędzie
//            (brak atrybutów showInputMessage / showErrorMessage)
//   A5 | B5  etykieta „Liczba dni (1–26)” | walidacja whole between 1 i 26: showInputMessage=1, ale PUSTY prompt;
//            showErrorMessage=1, ale PUSTY komunikat błędu
//   A6 | B6  etykieta „Data rozpoczęcia” | walidacja date ≥ 46023 (1.01.2026) z pełnym komunikatem wejściowym i o błędzie → poprawne
//   A7 | B7  etykieta „Rodzaj urlopu” | walidacja lista z innego arkusza zapisana w rozszerzeniu x14 (extLst), bez komunikatów
//   D10:D12  trzy pola do wypełnienia BEZ etykiet (C10:C12, D9 i E10:E12 puste) z walidacją decimal ≥ 0 bez komunikatów
//   A14 | B14 etykieta „Uwagi” | pole bez walidacji → poprawne
//   F3:F4    dwa pola do wypełnienia BEZ etykiet i BEZ walidacji (E3:E4 i F2 puste) → XL-FIELD-LABEL
// Arkusz 2 „Słowniki”:
//   A1:A4    tabela „TabelaRodzaje”: nagłówek „Rodzaj urlopu”, wartości Wypoczynkowy, Na żądanie, Okolicznościowy
public static class FormsXlsxFixture
{
    public static byte[] Build()
    {
        var fx = new FixtureXlsx { Title = "Formularz zgłoszenia urlopu", Language = "pl-PL" };
        var heading = fx.Styles.Xf(font: fx.Styles.Font(size: 14, bold: true));
        var input = fx.Styles.Xf(fill: fx.Styles.Fill("rgb=\"FFFFF2CC\""), locked: false);

        var form = fx.AddSheet("Formularz zgłoszenia");
        form.Protected = true;
        form.Columns(1, 2, 28);
        form.Text("A1", "Formularz zgłoszenia urlopu", heading);
        form.Text("A3", "Imię i nazwisko").Styled("B3", input);
        form.Text("A4", "Zgoda przełożonego").Styled("B4", input);
        form.Text("A5", "Liczba dni (1–26)").Styled("B5", input);
        form.Text("A6", "Data rozpoczęcia").Styled("B6", input);
        form.Text("A7", "Rodzaj urlopu").Styled("B7", input);
        form.Styled("D10", input).Styled("D11", input).Styled("D12", input);
        form.Text("A14", "Uwagi").Styled("B14", input);
        form.Styled("F3", input).Styled("F4", input);

        form.DataValidations.Add("<dataValidation type=\"list\" allowBlank=\"1\" sqref=\"B4\"><formula1>\"Tak,Nie\"</formula1></dataValidation>");
        form.DataValidations.Add(
            "<dataValidation type=\"whole\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" sqref=\"B5\"><formula1>1</formula1><formula2>26</formula2></dataValidation>");
        form.DataValidations.Add(
            "<dataValidation type=\"date\" operator=\"greaterThanOrEqual\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" errorTitle=\"Niepoprawna data\" "
            + "error=\"Wpisz datę nie wcześniejszą niż 1.01.2026 w formacie DD.MM.RRRR.\" promptTitle=\"Data rozpoczęcia urlopu\" "
            + "prompt=\"Wpisz datę w formacie DD.MM.RRRR, nie wcześniejszą niż 1.01.2026.\" sqref=\"B6\"><formula1>46023</formula1></dataValidation>");
        form.DataValidations.Add(
            "<dataValidation type=\"decimal\" operator=\"greaterThanOrEqual\" allowBlank=\"1\" sqref=\"D10:D12\"><formula1>0</formula1></dataValidation>");
        form.Extensions.Add(
            "<ext uri=\"{CCE6A557-97BC-4b89-ADB6-D9C93CAAB3DF}\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\">"
            + "<x14:dataValidations count=\"1\" xmlns:xm=\"http://schemas.microsoft.com/office/excel/2006/main\"><x14:dataValidation type=\"list\" allowBlank=\"1\">"
            + "<x14:formula1><xm:f>Słowniki!$A$2:$A$4</xm:f></x14:formula1><xm:sqref>B7</xm:sqref></x14:dataValidation></x14:dataValidations></ext>");

        var dictionaries = fx.AddSheet("Słowniki");
        dictionaries.Text("A1", "Rodzaj urlopu").Text("A2", "Wypoczynkowy").Text("A3", "Na żądanie").Text("A4", "Okolicznościowy");
        dictionaries.Table(new FxTable("TabelaRodzaje", "A1:A4", ["Rodzaj urlopu"]));
        return fx.Build();
    }
}
