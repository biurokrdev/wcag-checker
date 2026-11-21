using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Core;

// Zamiana kolorów SpreadsheetML na RRGGBB i kontrast WCAG.
public class XlsxColorTests
{
    private static readonly XlsxWorkbook Workbook = XlsxTestData.Workbook("empty");

    private static string Font(XlsxColor? color) => XlsxColors.Resolve(color, Workbook, XlsxColorRole.Font);

    private static string Fill(XlsxColor? color) => XlsxColors.Resolve(color, Workbook, XlsxColorRole.Fill);

    [Fact]
    public void Resolve_Rgb_Argb_IgnoresAlpha_AndUppercases()
    {
        Assert.Equal("999999", Font(new XlsxColor("FF999999", null, 0, null, false)));
        Assert.Equal("1F4E79", Font(new XlsxColor("001f4e79", null, 0, null, false)));
        Assert.Equal("ABCDEF", Font(new XlsxColor("abcdef", null, 0, null, false)));
    }

    [Fact]
    public void Resolve_MissingAutoAndInvalid_GiveRoleDefault()
    {
        Assert.Equal("000000", Font(null));
        Assert.Equal("FFFFFF", Fill(null));
        Assert.Equal("000000", Font(new XlsxColor(null, null, 0, null, true)));
        Assert.Equal("FFFFFF", Fill(new XlsxColor(null, null, 0, null, true)));
        // auto ma pierwszeństwo przed pozostałymi atrybutami
        Assert.Equal("000000", Font(new XlsxColor("FFFF0000", null, 0, null, true)));
        Assert.Equal("000000", Font(new XlsxColor("nie-kolor", null, 0, null, false)));
        Assert.Equal("FFFFFF", Fill(new XlsxColor(null, 99, 0, null, false)));
        Assert.Equal("000000", Font(new XlsxColor(null, null, 0, 200, false)));
    }

    [Fact]
    public void Resolve_Theme_IndexesAreSwappedRelativeToThemeFile()
    {
        // W pliku motywu: dk1, lt1, dk2, lt2; w arkuszu theme="0" to lt1, theme="1" to dk1, theme="2" to lt2, theme="3" to dk2.
        string[] expected = ["FFFFFF", "000000", "E7E6E6", "44546A", "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47", "0563C1", "954F72"];

        Assert.Equal(expected, Workbook.Theme.ColorsHex);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], Font(new XlsxColor(null, i, 0, null, false)));
        }
    }

    [Fact]
    public void Resolve_Theme_ReadFromFile_NotFromDefault()
    {
        // Motyw z innymi kolorami niż „Office”: dk1 = 111111, lt1 = EEEEEE, accent1 = 123456.
        var fx = new FixtureXlsx();
        fx.AddSheet("Dane").Text("A1", "x");
        var parts = FixtureXlsx.Parts(fx.Build());
        var theme = System.Text.Encoding.UTF8.GetString(parts["xl/theme/theme1.xml"])
            .Replace("lastClr=\"000000\"", "lastClr=\"111111\"").Replace("lastClr=\"FFFFFF\"", "lastClr=\"EEEEEE\"").Replace("4472C4", "123456");
        var workbook = XlsxTestData.Read(XlsxSecurityTests.Repack(parts, ("xl/theme/theme1.xml", theme)));

        Assert.Equal("EEEEEE", XlsxColors.Resolve(new XlsxColor(null, 0, 0, null, false), workbook, XlsxColorRole.Font));
        Assert.Equal("111111", XlsxColors.Resolve(new XlsxColor(null, 1, 0, null, false), workbook, XlsxColorRole.Font));
        Assert.Equal("123456", XlsxColors.Resolve(new XlsxColor(null, 4, 0, null, false), workbook, XlsxColorRole.Font));
    }

    // Wartości referencyjne: paleta „Kolory motywu” programu Excel dla motywu Office (wiersze „jaśniejszy 80/60/40%”,
    // „ciemniejszy 25/50%” oraz odcienie bieli i czerni) i odpowiadające im wartości tint zapisywane przez Excela
    // (0,7999…, 0,5999…, 0,3999…, −0,2499…, −0,4999…, −0,0499…, −0,1499…, −0,3499…, 0,4999…, 0,3499…, 0,2499…, 0,1499…, 0,0499…).
    // Źródło: wartości znane autorowi z plików i okna kolorów Excela, odtworzone z pamięci – NIE zostały w tej sesji
    // potwierdzone w programie Excel. Algorytm w XlsxColors.ApplyTint (HLS 0–240, obcięcie L' z tolerancją 0,01, zaokrąglenie
    // przy powrocie do RGB) dobrano tak, żeby odtwarzał wszystkie 45 zapamiętanych wartości palety (tu ich podzbiór); warianty
    // „zaokrąglaj L'” (34/45), „obcinaj L' bez tolerancji” (28/45) i zmiennoprzecinkowy (20/45, np. 4472C4 + 0,4 → 8FAADC,
    // czyli paleta PowerPointa) dają różnice o 1 na kanale.
    public static TheoryData<string, double, string> TintReference() => new()
    {
        { "4472C4", 0.79998168889431442, "D9E1F2" },
        { "4472C4", 0.59999389629810485, "B4C6E7" },
        { "4472C4", 0.39997558519241921, "8EA9DB" },
        { "4472C4", 0.4, "8EA9DB" },
        { "4472C4", -0.249977111117893, "305496" },
        { "4472C4", -0.499984740745262, "203764" },
        { "FFFFFF", -4.9989318521683403E-2, "F2F2F2" },
        { "FFFFFF", -0.14999847407452621, "D9D9D9" },
        { "FFFFFF", -0.249977111117893, "BFBFBF" },
        { "FFFFFF", -0.34998626667073579, "A6A6A6" },
        { "FFFFFF", -0.499984740745262, "808080" },
        { "000000", 0.499984740745262, "808080" },
        { "000000", 0.34998626667073579, "595959" },
        { "000000", 0.249977111117893, "404040" },
        { "000000", 0.14999847407452621, "262626" },
        { "000000", 4.9989318521683403E-2, "0D0D0D" },
        { "000000", 0.5, "808080" },
        { "ED7D31", 0.79998168889431442, "FCE4D6" },
        { "ED7D31", 0.39997558519241921, "F4B084" },
        { "ED7D31", -0.249977111117893, "C65911" },
        { "70AD47", 0.59999389629810485, "C6E0B4" },
        { "70AD47", -0.499984740745262, "375623" },
        { "44546A", 0.79998168889431442, "D6DCE4" },
        { "44546A", -0.249977111117893, "333F4F" },
        { "E7E6E6", -0.249977111117893, "AEAAAA" },
        { "FFC000", 0.59999389629810485, "FFE699" },
        { "5B9BD5", 0.39997558519241921, "9BC2E6" },
        { "5B9BD5", -0.499984740745262, "1F4E78" },
        { "A5A5A5", 0.79998168889431442, "EDEDED" },
        { "A5A5A5", -0.249977111117893, "7B7B7B" },
    };

    [Theory]
    [MemberData(nameof(TintReference))]
    public void ApplyTint_MatchesExcelThemePalette(string hex, double tint, string expected)
    {
        Assert.Equal(expected, XlsxColors.ApplyTint(hex, tint));
    }

    // Wartości wg własnego obliczenia tym samym algorytmem (bez zewnętrznego wzorca) – pilnują regresji, nie zgodności z Excelem.
    [Theory]
    [InlineData("4472C4", 0, "4472C4")]
    [InlineData("4472C4", 1, "FFFFFF")]
    [InlineData("4472C4", -1, "000000")]
    [InlineData("808080", 0.5, "BFBFBF")]
    [InlineData("FF0000", -0.5, "800000")]
    public void ApplyTint_OwnCalculation(string hex, double tint, string expected)
    {
        Assert.Equal(expected, XlsxColors.ApplyTint(hex, tint));
    }

    [Fact]
    public void ApplyTint_AlwaysReturnsValidColor()
    {
        foreach (var hex in new[] { "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "123456", "FEDCBA", "7F7F7F", "010101", "FEFEFE" })
        {
            for (var tint = -1.0; tint <= 1.0; tint += 0.05)
            {
                var result = XlsxColors.ApplyTint(hex, tint);
                Assert.Equal(result, XlsxColors.NormalizeHex(result));
                // Rozjaśnianie nie przyciemnia, przyciemnianie nie rozjaśnia.
                var before = WcagContrast.RelativeLuminance(hex);
                var after = WcagContrast.RelativeLuminance(result);
                Assert.True(tint >= 0 ? after >= before - 0.02 : after <= before + 0.02, $"{hex} tint {tint} → {result}");
            }
        }
    }

    [Fact]
    public void Resolve_ThemeWithTint_AndRgbWithTint()
    {
        Assert.Equal("8EA9DB", Font(new XlsxColor(null, 4, 0.39997558519241921, null, false)));
        Assert.Equal("F2F2F2", Fill(new XlsxColor(null, 0, -4.9989318521683403E-2, null, false)));
        Assert.Equal("808080", Font(new XlsxColor(null, 1, 0.499984740745262, null, false)));
        Assert.Equal("808080", Font(new XlsxColor("FFFFFFFF", null, -0.499984740745262, null, false)));
    }

    [Fact]
    public void Resolve_Indexed_DefaultPalette()
    {
        var palette = XlsxColors.DefaultIndexedPalette;

        Assert.Equal(64, palette.Count);
        // Paleta domyślna wg ECMA-376 część 1, 18.8.27 (indexedColors): 0–7 kolory podstawowe, 8–15 ich powtórzenie.
        (int Index, string Hex)[] reference =
        [
            (0, "000000"), (1, "FFFFFF"), (2, "FF0000"), (3, "00FF00"), (4, "0000FF"), (5, "FFFF00"), (6, "FF00FF"), (7, "00FFFF"),
            (8, "000000"), (9, "FFFFFF"), (10, "FF0000"), (13, "FFFF00"), (16, "800000"), (17, "008000"), (18, "000080"),
            (22, "C0C0C0"), (23, "808080"), (24, "9999FF"), (31, "CCCCFF"), (40, "00CCFF"), (47, "FFCC99"), (48, "3366FF"),
            (53, "FF6600"), (55, "969696"), (56, "003366"), (62, "333399"), (63, "333333"),
        ];
        foreach (var (index, hex) in reference)
        {
            Assert.Equal(hex, palette[index]);
            Assert.Equal(hex, Font(new XlsxColor(null, null, 0, index, false)));
        }

        Assert.All(palette, hex => Assert.Equal(hex, XlsxColors.NormalizeHex(hex)));
        Assert.Equal(palette.Take(8), palette.Skip(8).Take(8));
    }

    [Fact]
    public void Resolve_Indexed_SystemColors64And65()
    {
        Assert.Equal("000000", Fill(new XlsxColor(null, null, 0, 64, false)));
        Assert.Equal("FFFFFF", Font(new XlsxColor(null, null, 0, 65, false)));
    }

    [Fact]
    public void Resolve_Indexed_PaletteFromFileOverridesDefault()
    {
        var edge = XlsxTestData.Workbook("edge");

        Assert.Equal("006100", XlsxColors.Resolve(new XlsxColor(null, null, 0, 10, false), edge, XlsxColorRole.Font));
        Assert.Equal("FF0000", XlsxColors.Resolve(new XlsxColor(null, null, 0, 2, false), edge, XlsxColorRole.Font));
        Assert.Equal("000000", XlsxColors.Resolve(new XlsxColor(null, null, 0, 64, false), edge, XlsxColorRole.Fill));
        Assert.Equal("FF0000", Font(new XlsxColor(null, null, 0, 10, false)));
    }

    [Fact]
    public void EffectiveBackground_Patterns()
    {
        XlsxCellFormat Format(XlsxFill fill) => new(0, new XlsxFont("Calibri", 11, false, false, false, false, null), fill, 0, "General", false, true, null, false);
        var black = new XlsxColor("FF000000", null, 0, null, false);
        var white = new XlsxColor("FFFFFFFF", null, 0, null, false);
        var blue = new XlsxColor("FF0000FF", null, 0, null, false);

        Assert.Equal("FFFFFF", XlsxColors.EffectiveBackground(Format(new XlsxFill("none", blue, null)), Workbook));
        Assert.Equal("0000FF", XlsxColors.EffectiveBackground(Format(new XlsxFill("solid", blue, white)), Workbook));
        Assert.Equal("DFDFDF", XlsxColors.EffectiveBackground(Format(new XlsxFill("gray125", black, white)), Workbook));
        Assert.Equal("DFDFDF", XlsxColors.EffectiveBackground(Format(new XlsxFill("gray125", null, null)), Workbook));
        Assert.Equal("808080", XlsxColors.EffectiveBackground(Format(new XlsxFill("mediumGray", black, white)), Workbook));
        Assert.Equal("404040", XlsxColors.EffectiveBackground(Format(new XlsxFill("darkGray", black, white)), Workbook));
        Assert.Equal("BFBFFF", XlsxColors.EffectiveBackground(Format(new XlsxFill("lightGray", blue, white)), Workbook));
        Assert.Equal("0000FF", XlsxColors.EffectiveBackground(Format(new XlsxFill("gradient", blue, white)), Workbook));

        Assert.False(XlsxColors.IsApproximateBackground(new XlsxFill("none", null, null)));
        Assert.False(XlsxColors.IsApproximateBackground(new XlsxFill("solid", blue, null)));
        Assert.True(XlsxColors.IsApproximateBackground(new XlsxFill("gray125", null, null)));
        Assert.True(XlsxColors.IsApproximateBackground(new XlsxFill("gradient", blue, white)));
        Assert.False(XlsxColors.IsApproximateBackground((XlsxFill?)null));
    }

    // ---------- kontrast ----------

    [Theory]
    [InlineData("000000", "FFFFFF", 21.0)]
    [InlineData("FFFFFF", "000000", 21.0)]
    [InlineData("FFFFFF", "FFFFFF", 1.0)]
    [InlineData("767676", "FFFFFF", 4.54)]
    [InlineData("777777", "FFFFFF", 4.48)]
    [InlineData("949494", "FFFFFF", 3.03)]
    [InlineData("959595", "FFFFFF", 3.00)]
    [InlineData("999999", "FFFFFF", 2.85)]
    public void Contrast_Ratio_ReferenceValues(string a, string b, double expected)
    {
        // Wartości referencyjne do 2 miejsc: wzór z definicji „contrast ratio” WCAG; 767676 to najjaśniejszy szary spełniający
        // 4,5:1 na bieli (powszechnie cytowane 4,54:1), 949494 – najjaśniejszy spełniający 3:1 (3,03:1).
        Assert.Equal(expected, WcagContrast.Ratio(a, b), 2);
    }

    [Fact]
    public void Contrast_ThresholdNeighbours_AreOnTheRightSide()
    {
        Assert.True(WcagContrast.Ratio("767676", "FFFFFF") >= 4.5);
        Assert.True(WcagContrast.Ratio("777777", "FFFFFF") < 4.5);
        Assert.True(WcagContrast.Ratio("949494", "FFFFFF") >= 3.0);
        // 959595 daje 2,9953 – po zaokrągleniu do dwóch miejsc „3,00”, a mimo to nie spełnia progu 3:1.
        Assert.True(WcagContrast.Ratio("959595", "FFFFFF") < 3.0);
        Assert.Equal(2.9953, WcagContrast.Ratio("959595", "FFFFFF"), 4);
    }

    [Fact]
    public void Contrast_RelativeLuminance_Extremes()
    {
        Assert.Equal(0, WcagContrast.RelativeLuminance("000000"), 10);
        Assert.Equal(1, WcagContrast.RelativeLuminance("FFFFFF"), 10);
        Assert.Equal(0.2126, WcagContrast.RelativeLuminance("FF0000"), 4);
        Assert.Equal(0.7152, WcagContrast.RelativeLuminance("00FF00"), 4);
        Assert.Equal(0.0722, WcagContrast.RelativeLuminance("0000FF"), 4);
        Assert.Equal(WcagContrast.RelativeLuminance("FFAA00FF"), WcagContrast.RelativeLuminance("#aa00ff"));
    }

    [Fact]
    public void Contrast_LinearizationThresholds_0_04045_And_0_03928_AgreeForAll8BitValues()
    {
        // WCAG 2.x podaje w definicji próg 0,03928, norma sRGB 0,04045. Dla kanału 8-bitowego wynik jest identyczny.
        for (var channel = 0; channel <= 255; channel++)
        {
            Assert.Equal(WcagContrast.Linear(channel, 0.03928), WcagContrast.Linear(channel, 0.04045));
        }

        Assert.True(10 / 255.0 < 0.03928 && 11 / 255.0 > 0.04045);
    }

    [Theory]
    [InlineData(21.0, "21,0:1")]
    [InlineData(1.0, "1,0:1")]
    [InlineData(2.849, "2,8:1")]
    [InlineData(4.4781, "4,4:1")]
    [InlineData(4.4999999, "4,4:1")]
    [InlineData(4.5, "4,5:1")]
    [InlineData(4.5422, "4,5:1")]
    [InlineData(2.9953, "2,9:1")]
    [InlineData(2.99999, "2,9:1")]
    [InlineData(3.0, "3,0:1")]
    [InlineData(3.0335, "3,0:1")]
    [InlineData(6.99, "6,9:1")]
    public void Contrast_Format_TruncatesDown_NeverRoundsUpThroughThreshold(double ratio, string expected)
    {
        Assert.Equal(expected, WcagContrast.Format(ratio));
    }

    [Fact]
    public void Contrast_Format_OfComputedRatios()
    {
        Assert.Equal("21,0:1", WcagContrast.Format(WcagContrast.Ratio("000000", "FFFFFF")));
        Assert.Equal("4,4:1", WcagContrast.Format(WcagContrast.Ratio("777777", "FFFFFF")));
        Assert.Equal("2,9:1", WcagContrast.Format(WcagContrast.Ratio("959595", "FFFFFF")));
        Assert.Equal("2,8:1", WcagContrast.Format(WcagContrast.Ratio("999999", "FFFFFF")));
    }

    [Theory]
    [InlineData(18, false, true)]
    [InlineData(17.9, false, false)]
    [InlineData(14, true, true)]
    [InlineData(13.9, true, false)]
    [InlineData(14, false, false)]
    [InlineData(11, true, false)]
    [InlineData(24, true, true)]
    public void Contrast_LargeText_And_RequiredRatio(double size, bool bold, bool large)
    {
        Assert.Equal(large, WcagContrast.IsLargeText(size, bold));
        Assert.Equal(large ? 3.0 : 4.5, WcagContrast.RequiredRatio(size, bold));
    }

    [Fact]
    public void Contrast_InvalidColor_Throws()
    {
        Assert.Throws<FormatException>(() => WcagContrast.Ratio("xyz", "FFFFFF"));
    }
}
