namespace D2.Wcag.Xlsx;

/// <summary>
/// Struktura skoroszytu tylko do odczytu – do pokazania hierarchii (arkusze, Tabele, wykresy, obrazy i kształty, kontrolki,
/// nazwy zdefiniowane). Budowana z modelu, który audyt i tak wczytał (<see cref="XlsxWorkbook"/>): bez dodatkowego parsowania
/// pliku i bez wpływu na zgłoszenia, checklistę ani kryteria. Klucze (<c>Key</c>) to <see cref="XlsxLocation.Key"/> tego samego
/// obiektu, więc zgłoszenie wskazuje węzeł przez <c>Location.Key</c> (komórki i zakresy – przez indeks arkusza).
/// Teksty z pliku (tytuły, wartości nazw) są skracane jak fragmenty zgłoszeń (<see cref="XlsxLocation.MaxSnippetLength"/>).
/// </summary>
public sealed record XlsxWorkbookOutline(IReadOnlyList<XlsxOutlineSheet> Sheets, IReadOnlyList<XlsxOutlineDefinedName> DefinedNames)
{
    /// <summary>Klucz węzła „Skoroszyt” (= <see cref="XlsxLocation.Workbook"/>().Key).</summary>
    public static string WorkbookKey { get; } = XlsxLocation.Workbook().Key;

    public static XlsxWorkbookOutline From(XlsxWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var sheets = workbook.Sheets.Select(OfSheet).ToList();
        var names = workbook.DefinedNames
            .Select(n => new XlsxOutlineDefinedName(
                n.Name,
                XlsxLocation.Shorten(n.Value),
                n.LocalSheetIndex is { } local && local >= 0 && local < workbook.Sheets.Count ? workbook.Sheets[local].Name : null,
                n.Hidden,
                XlsxLocation.OfDefinedName(n.Name).Key))
            .ToList();
        return new XlsxWorkbookOutline(sheets, names);
    }

    private static XlsxOutlineSheet OfSheet(XlsxSheet sheet)
    {
        var tables = sheet.Tables
            .Select(t => new XlsxOutlineTable(
                string.IsNullOrEmpty(t.DisplayName) ? t.Name : t.DisplayName,
                t.Ref.ToString(),
                t.HeaderRowCount > 0,
                t.TotalsRowCount > 0,
                t.ColumnNames.Count,
                !string.IsNullOrWhiteSpace(t.AltTitle) || !string.IsNullOrWhiteSpace(t.AltDescription),
                XlsxLocation.OfTable(sheet, t).Key))
            .ToList();
        var charts = sheet.Charts
            .Select(c => new XlsxOutlineChart(
                c.Name,
                string.IsNullOrWhiteSpace(c.Title) ? null : XlsxLocation.Shorten(c.Title),
                c.ChartType,
                HasText(c.Description) || HasText(c.Frame?.Description) || HasText(c.Frame?.Title),
                c.Frame?.Decorative == true,
                c.Frame?.Anchor?.ToString(),
                XlsxLocation.OfChart(sheet, c).Key))
            .ToList();
        // Ramki wykresów są na liście wykresów, kontrolki – na liście kontrolek.
        var drawings = sheet.Drawings
            .Where(d => d.Type is not (XlsxDrawingType.Chart or XlsxDrawingType.Control))
            .Select(d => new XlsxOutlineDrawing(
                d.Name,
                d.Type,
                HasText(d.Description) || HasText(d.Title),
                d.Decorative,
                d.Hidden,
                d.IsGroup,
                d.Anchor?.ToString(),
                XlsxLocation.OfDrawing(sheet, d).Key))
            .ToList();
        var controls = sheet.Controls
            .Select(c => new XlsxOutlineControl(
                c.Name,
                c.Kind,
                c.IsActiveX,
                HasText(c.AltText),
                string.IsNullOrWhiteSpace(c.Label) ? null : XlsxLocation.Shorten(c.Label),
                c.Anchor?.ToString(),
                XlsxLocation.OfControl(sheet, c).Key))
            .ToList();
        return new XlsxOutlineSheet(sheet.Name, sheet.Index, sheet.State, sheet.IsChartSheet, sheet.UsedRange?.ToString(),
            XlsxLocation.OfSheet(sheet).Key, tables, charts, drawings, controls);
    }

    private static bool HasText(string? text) => !string.IsNullOrWhiteSpace(text);
}

/// <summary>Arkusz w kolejności skoroszytu; <see cref="State"/> mówi, czy jest ukryty (także „bardzo ukryty”).</summary>
public sealed record XlsxOutlineSheet(string Name, int Index, XlsxSheetState State, bool IsChartSheet, string? UsedRange, string Key,
    IReadOnlyList<XlsxOutlineTable> Tables, IReadOnlyList<XlsxOutlineChart> Charts, IReadOnlyList<XlsxOutlineDrawing> Drawings,
    IReadOnlyList<XlsxOutlineControl> Controls);

/// <summary>Tabela Excela: nazwa wyświetlana, zakres, czy ma wiersz nagłówków i wiersz sumy, liczba kolumn, tekst alternatywny.</summary>
public sealed record XlsxOutlineTable(string Name, string Range, bool HasHeaderRow, bool HasTotalsRow, int Columns, bool HasAltText, string Key);

/// <summary>Wykres na arkuszu: tytuł (skrócony), typ, czy ma opis alternatywny, czy oznaczony jako dekoracyjny.</summary>
public sealed record XlsxOutlineChart(string Name, string? Title, string ChartType, bool HasAltText, bool Decorative, string? Anchor, string Key);

/// <summary>Obraz, kształt, pole tekstowe albo grupa; <see cref="HasAltText"/> – opis albo tytuł alternatywny.</summary>
public sealed record XlsxOutlineDrawing(string Name, XlsxDrawingType Type, bool HasAltText, bool Decorative, bool Hidden, bool IsGroup,
    string? Anchor, string Key);

/// <summary>Kontrolka formularza albo ActiveX.</summary>
public sealed record XlsxOutlineControl(string Name, string Kind, bool IsActiveX, bool HasAltText, string? Label, string? Anchor, string Key);

/// <summary>Nazwa zdefiniowana (nazwany zakres); <see cref="Scope"/> – arkusz dla nazwy lokalnej, <c>null</c> dla skoroszytu.</summary>
public sealed record XlsxOutlineDefinedName(string Name, string Value, string? Scope, bool Hidden, string Key);
