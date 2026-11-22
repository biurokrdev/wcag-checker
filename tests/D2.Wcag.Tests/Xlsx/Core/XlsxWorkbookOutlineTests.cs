using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Core;

// Struktura skoroszytu (XlsxAuditResult.Outline) na fixturach tests/fixtures/xlsx: dane tylko do odczytu dla zakładki
// „Hierarchia dokumentu”. Klucze węzłów muszą być kluczami lokalizacji zgłoszeń tych samych obiektów.
public class XlsxWorkbookOutlineTests
{
    private static XlsxAuditResult Audit(string fixture) => new WcagXlsxAuditor().Audit(XlsxSnapshotCases.Load(fixture));

    private static XlsxWorkbookOutline Outline(string fixture) => Audit(fixture).Outline ?? throw new InvalidOperationException("Brak struktury.");

    [Fact]
    public void Structure_SheetsInWorkbookOrder_HiddenAndVeryHiddenFlagged()
    {
        var outline = Outline("structure");

        Assert.Equal(["Arkusz1", "Sheet2", "Dane ukryte", "Słownik", "Koszty", "Tabele", "Późny start", "Q"], outline.Sheets.Select(s => s.Name));
        Assert.Equal(Enumerable.Range(0, 8), outline.Sheets.Select(s => s.Index));
        Assert.Equal(XlsxSheetState.Hidden, outline.Sheets[2].State);
        Assert.Equal(XlsxSheetState.VeryHidden, outline.Sheets[3].State);
        Assert.All(outline.Sheets.Where(s => s.Index is not (2 or 3)), s => Assert.Equal(XlsxSheetState.Visible, s.State));
    }

    [Fact]
    public void Structure_TableHeaderRowPresence()
    {
        var tables = Outline("structure").Sheets.Single(s => s.Name == "Tabele").Tables;

        var withoutHeader = Assert.Single(tables, t => t.Name == "Tabela1");
        Assert.False(withoutHeader.HasHeaderRow);
        Assert.Equal("A1:C4", withoutHeader.Range);
        var withHeader = Assert.Single(tables, t => t.Name == "Tabela2");
        Assert.True(withHeader.HasHeaderRow);
        Assert.Equal("E1:G4", withHeader.Range);
    }

    [Fact]
    public void Structure_DefinedNames()
    {
        var name = Assert.Single(Outline("structure").DefinedNames);

        Assert.Equal("Zakres_Sprzedaz", name.Name);
        Assert.Equal("Arkusz1!$A$1:$C$5", name.Value);
        Assert.Null(name.Scope);
        Assert.False(name.Hidden);
        Assert.Equal(XlsxLocation.OfDefinedName("Zakres_Sprzedaz").Key, name.Key);
    }

    [Fact]
    public void Edge_LocalAndHiddenDefinedNames_HaveScopeAndFlag()
    {
        var names = Outline("edge").DefinedNames;

        Assert.Equal("Q1'25", Assert.Single(names, n => n.Name == "Lokalna").Scope);
        Assert.Null(Assert.Single(names, n => n.Name == "Zakres_Duży").Scope);
        Assert.Contains(names, n => n.Name == "_xlnm._FilterDatabase" && n.Scope == "Duży");
        Assert.True(Assert.Single(Outline("edge").Sheets, s => s.Name == "Wykres arkusz").IsChartSheet);
    }

    [Fact]
    public void NonText_ImageAltPresence_DecorativeFlag_ChartsAndControls()
    {
        var outline = Outline("nontext");
        var images = outline.Sheets[0].Drawings;

        Assert.False(Assert.Single(images, d => d.Name == "Obraz 1").HasAltText);
        var decorative = Assert.Single(images, d => d.Name == "Obraz 2");
        Assert.True(decorative.Decorative);
        Assert.True(Assert.Single(images, d => d.Name == "Obraz 3").HasAltText);
        Assert.Equal(XlsxDrawingType.TextBox, Assert.Single(images, d => d.Name == "pole tekstowe 6").Type);
        Assert.True(Assert.Single(images, d => d.Name == "Grupa 8").IsGroup);
        // Ramki wykresów i kontrolki nie dublują się na liście obrazów i kształtów.
        Assert.DoesNotContain(outline.Sheets.SelectMany(s => s.Drawings), d => d.Type is XlsxDrawingType.Chart or XlsxDrawingType.Control);

        var charts = outline.Sheets.Single(s => s.Name == "Wykresy").Charts;
        Assert.False(Assert.Single(charts, c => c.Name == "Wykres 1").HasAltText);
        Assert.True(Assert.Single(charts, c => c.Name == "Wykres 2").HasAltText);

        var controls = outline.Sheets.Single(s => s.Name == "Kontrolki").Controls;
        Assert.Equal(["Check Box 1", "Button 2", "Drop Down 3", "CommandButton1"], controls.Select(c => c.Name));
        Assert.True(controls[3].IsActiveX);
    }

    [Fact]
    public void Clean_ImageWithAlt_DecorativeImage_TableWithHeader()
    {
        var outline = Outline("clean");

        Assert.True(Assert.Single(outline.Sheets[0].Drawings).HasAltText);
        Assert.True(Assert.Single(outline.Sheets[1].Drawings).Decorative);
        Assert.True(Assert.Single(outline.Sheets[0].Tables).HasHeaderRow);
        Assert.Empty(outline.DefinedNames);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void EveryFinding_OnAnObject_PointsAtAnOutlineNodeByKey(string fixture)
    {
        var result = Audit(fixture);
        var outline = result.Outline!;
        var keys = outline.Sheets
            .SelectMany(s => new[] { s.Key }
                .Concat(s.Tables.Select(t => t.Key))
                .Concat(s.Charts.Select(c => c.Key))
                .Concat(s.Drawings.Select(d => d.Key))
                .Concat(s.Controls.Select(c => c.Key)))
            .Concat(outline.DefinedNames.Select(n => n.Key))
            .Append(XlsxWorkbookOutline.WorkbookKey)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            result.Findings.Where(f => f.Location.Kind is not (XlsxLocationKind.Cell or XlsxLocationKind.Range)),
            f => Assert.Contains(f.Location.Key, keys));
        // Komórki i zakresy wskazują arkusz po indeksie.
        Assert.All(
            result.Findings.Where(f => f.Location.Kind is XlsxLocationKind.Cell or XlsxLocationKind.Range),
            f => Assert.Contains(outline.Sheets, s => s.Index == f.Location.SheetIndex));
    }

    public static IEnumerable<object[]> Fixtures() => XlsxSnapshotCases.AllFixtures();

    [Fact]
    public void UnreadableFile_HasNoOutline()
    {
        var result = new WcagXlsxAuditor().Audit("to nie jest skoroszyt"u8.ToArray());

        Assert.False(result.IsReadable);
        Assert.Null(result.Outline);
    }
}
