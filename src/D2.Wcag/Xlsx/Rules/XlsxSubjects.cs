namespace D2.Wcag.Xlsx.Rules;

/// <summary>
/// Wspólne predykaty <see cref="XlsxRuleDescriptor.HasSubject"/>: czy w skoroszycie jest treść, którą kontrola bada. Predykaty
/// kończą się na pierwszym trafieniu – w typowym skoroszycie kosztują kilka porównań.
/// </summary>
internal static class XlsxSubjects
{
    /// <summary>Jakakolwiek komórka z wartością zapisaną w pliku.</summary>
    public static bool HasCellValues(XlsxWorkbook workbook) => workbook.Sheets.Any(s => s.Cells.Any(c => c.HasValue));

    /// <summary>Komórka z wartością na widocznym arkuszu (nie arkuszu wykresu).</summary>
    public static bool HasVisibleCellValues(XlsxWorkbook workbook) =>
        workbook.Sheets.Any(s => s.State == XlsxSheetState.Visible && !s.IsChartSheet && s.Cells.Any(c => c.HasValue));

    /// <summary>Tekst w komórkach, obiektach, komentarzach albo komunikatach walidacji danych.</summary>
    public static bool HasAnyText(XlsxWorkbook workbook) =>
        HasCellValues(workbook)
        || workbook.Sheets.Any(s => s.Comments.Count > 0
            || s.Drawings.Any(d => !string.IsNullOrWhiteSpace(d.Text))
            || s.DataValidations.Any(v => !string.IsNullOrWhiteSpace(v.Prompt) || !string.IsNullOrWhiteSpace(v.Error)));

    public static bool HasTables(XlsxWorkbook workbook) => workbook.Sheets.Any(s => s.Tables.Count > 0);

    public static bool HasTablesWithHeader(XlsxWorkbook workbook) => workbook.Sheets.Any(s => s.Tables.Any(t => t.HeaderRowCount > 0));
}
