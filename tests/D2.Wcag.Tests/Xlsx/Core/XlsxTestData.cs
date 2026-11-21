using System.Collections.Concurrent;
using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Core;

/// <summary>Wspólne dane testów XLSX: modele fixtur (czytane raz) i odczyt z pełną diagnostyką błędu.</summary>
internal static class XlsxTestData
{
    private static readonly ConcurrentDictionary<string, Lazy<XlsxWorkbook>> Workbooks = new();

    /// <summary>Model fixtury z generatora (ten sam obiekt dla wszystkich testów – model jest tylko do odczytu).</summary>
    public static XlsxWorkbook Workbook(string fixture) =>
        Workbooks.GetOrAdd(fixture, name => new Lazy<XlsxWorkbook>(() => Read(XlsxFixtureCatalog.Build(name)))).Value;

    /// <summary>Odczyt skoroszytu; przy odmowie test dostaje także wyjątek źródłowy.</summary>
    public static XlsxWorkbook Read(byte[] content, XlsxLimits? limits = null)
    {
        try
        {
            return XlsxPackageReader.Read(content, limits);
        }
        catch (XlsxReadException exception)
        {
            throw new InvalidOperationException($"{exception.Failure}: {exception.Message}\n{exception.InnerException}", exception);
        }
    }

    public static XlsxSheet Sheet(this XlsxWorkbook workbook, string name) => workbook.Sheets.Single(s => s.Name == name);

    public static string Text(this XlsxSheet sheet, string reference) => sheet.TextAt(CellRef.Parse(reference));

    public static XlsxCell CellAt(this XlsxSheet sheet, string reference) =>
        sheet.Cell(CellRef.Parse(reference)) ?? throw new InvalidOperationException($"Brak komórki {reference} w arkuszu „{sheet.Name}”.");

    public static XlsxCellFormat FormatAt(this XlsxWorkbook workbook, string sheet, string reference) =>
        workbook.Styles.Format(workbook.Sheet(sheet).CellAt(reference).StyleIndex);
}
