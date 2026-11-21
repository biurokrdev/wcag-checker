using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Core;

// Zabezpieczenie nowej ścieżki (Resolved) względem starej (ResolveRun/ResolveParagraph): wszędzie tam, gdzie nowa
// ścieżka celowo nie różni się semantyką, wartości muszą być równe. Celowe różnice, pomijane w porównaniu:
//  - wartość pochodzi ze stylu tabeli albo z poziomu numeracji (stara ścieżka tych warstw nie zna),
//  - kolor zapisany jako kolor motywu (stara ścieżka bierze w:val, nowa rozwiązuje motyw),
//  - przełącznik (pogrubienie, wersaliki) ustawiony na więcej niż jednym poziomie stylów (nowa ścieżka: XOR).
public class ResolverParityTests
{
    public static IEnumerable<object[]> Documents() => SnapshotCases.AllDocuments();

    [Theory]
    [MemberData(nameof(Documents))]
    public void ResolvedRuns_MatchLegacyRunFormat(string name)
    {
        var docx = LocationRoundTripTests.Open(name);
        using var package = docx.Package;
        var compared = 0;

        foreach (var story in docx.Stories())
        {
            foreach (var paragraph in docx.ResolvedParagraphs(docx.StoryKey(story.Part)))
            {
                foreach (var run in paragraph.Runs)
                {
                    var legacy = docx.ResolveRun(run.Element);
                    var format = run.Format;
                    var where = $"{name} {run.Location.Key}";

                    if (Comparable(format, FormatProps.Bold) && !ToggleOnSeveralLevels(docx, paragraph, run, s => s.StyleRunProperties?.Bold is not null))
                    {
                        Assert.True(legacy.Bold == format.Flag(FormatProps.Bold), $"{where}: pogrubienie");
                    }

                    if (Comparable(format, FormatProps.Caps) && !ToggleOnSeveralLevels(docx, paragraph, run, s => s.StyleRunProperties?.Caps is not null))
                    {
                        Assert.True(legacy.Caps == format.Flag(FormatProps.Caps), $"{where}: wersaliki");
                    }

                    if (Comparable(format, FormatProps.Underline))
                    {
                        Assert.True(legacy.Underline == format.Flag(FormatProps.Underline), $"{where}: podkreślenie");
                    }

                    if (Comparable(format, FormatProps.Size))
                    {
                        Assert.True(Math.Abs(legacy.SizePt - format.Number(FormatProps.Size)!.Value) < 0.001, $"{where}: rozmiar {legacy.SizePt} / {format.Text(FormatProps.Size)}");
                    }

                    if (Comparable(format, FormatProps.Color) && !UsesThemeColor(docx, run, format.Get(FormatProps.Color)!))
                    {
                        Assert.True(Same(legacy.ColorHex, format.Text(FormatProps.Color), "auto"), $"{where}: kolor {legacy.ColorHex} / {format.Text(FormatProps.Color)}");
                    }

                    if (Comparable(format, FormatProps.Highlight))
                    {
                        Assert.True(Same(legacy.Highlight, format.Text(FormatProps.Highlight), "none"), $"{where}: wyróżnienie");
                    }

                    if (Comparable(format, FormatProps.Shading))
                    {
                        Assert.True(Same(legacy.ShadingFill, format.Text(FormatProps.Shading), "auto"), $"{where}: cieniowanie");
                    }

                    if (Comparable(format, FormatProps.Font))
                    {
                        Assert.True(legacy.FontName == format.Text(FormatProps.Font), $"{where}: czcionka {legacy.FontName} / {format.Text(FormatProps.Font)}");
                    }

                    if (Comparable(format, FormatProps.Language))
                    {
                        Assert.True(legacy.Language == format.Text(FormatProps.Language), $"{where}: język");
                    }

                    compared++;
                }
            }
        }

        Assert.True(compared > 0);
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void ResolvedParagraphs_MatchLegacyParagraphFormat(string name)
    {
        var docx = LocationRoundTripTests.Open(name);
        using var package = docx.Package;
        var compared = 0;

        foreach (var story in docx.Stories())
        {
            foreach (var paragraph in docx.ResolvedParagraphs(docx.StoryKey(story.Part)))
            {
                var legacy = docx.ResolveParagraph(paragraph.Element);
                var format = paragraph.Format;
                var where = $"{name} {paragraph.Location.Key}";

                if (Comparable(format, FormatProps.NumId))
                {
                    var numId = format.Number(FormatProps.NumId) is > 0 and var id ? (int?)id : null;
                    Assert.True(legacy.NumberingId == numId, $"{where}: numeracja {legacy.NumberingId} / {numId}");
                }

                if (Comparable(format, FormatProps.Alignment))
                {
                    var alignment = format.Get(FormatProps.Alignment)!;
                    Assert.True(legacy.Alignment == (alignment.Source == ValueSource.BuiltIn ? null : alignment.Value), $"{where}: wyrównanie {legacy.Alignment} / {alignment.Value}");
                    Assert.True(legacy.Justified == alignment.Value is "both" or "distribute", $"{where}: justowanie");
                }

                if (Comparable(format, FormatProps.ParagraphShading))
                {
                    Assert.True(Same(legacy.ShadingFill, format.Text(FormatProps.ParagraphShading), "auto"), $"{where}: cieniowanie akapitu");
                }

                if (Comparable(format, FormatProps.SpaceBefore))
                {
                    Assert.True(SameNumber(legacy.SpacingBeforePt, format.Get(FormatProps.SpaceBefore)!), $"{where}: odstęp przed");
                }

                if (Comparable(format, FormatProps.SpaceAfter))
                {
                    Assert.True(SameNumber(legacy.SpacingAfterPt, format.Get(FormatProps.SpaceAfter)!), $"{where}: odstęp po");
                }

                if (Comparable(format, FormatProps.LineSpacing) && Comparable(format, FormatProps.LineRule))
                {
                    var expected = format.Get(FormatProps.LineSpacing)!.Source == ValueSource.BuiltIn ? null : paragraph.LineSpacing;
                    Assert.True(
                        legacy.LineSpacing is null == expected is null && (expected is null || Math.Abs(legacy.LineSpacing!.Value - expected.Value) < 0.001),
                        $"{where}: interlinia {legacy.LineSpacing} / {expected}");
                }

                Assert.Equal(docx.HeadingLevel(paragraph.Element), paragraph.HeadingLevel);
                compared++;
            }
        }

        Assert.True(compared > 0);
    }

    // Wspólny detektor fałszywych nagłówków na nowej ścieżce wskazuje te same akapity co reguła WCAG na starej.
    [Theory]
    [MemberData(nameof(Documents))]
    public void FakeHeadingCandidates_MatchTheWcagFakeHeadingRule(string name)
    {
        var bytes = SnapshotCases.Load(name);
        var result = new WcagDocxValidator().Validate(bytes, new WcagValidationProfile().WithStandards("wcag/2.1"));
        var expected = result.Issues.Where(i => i.Code == IssueCodes.FakeHeading).SelectMany(i => i.Locations).Select(l => l.Key).ToList();
        var docx = LocationRoundTripTests.Open(bytes);
        using var package = docx.Package;

        var candidates = docx.FakeHeadingCandidates();

        Assert.Equal(expected, candidates.Select(c => c.Location.Key));
        Assert.Same(candidates, docx.FakeHeadingCandidates());
        Assert.All(candidates, c => Assert.Null(c.HeadingLevel));
    }

    private static bool Comparable(ResolvedFormat format, string property) =>
        format.Get(property) is not { Source: ValueSource.TableStyle or ValueSource.Numbering };

    private static bool Same(string? legacy, string? resolved, string none) =>
        string.Equals(legacy, resolved == none ? null : resolved, StringComparison.OrdinalIgnoreCase);

    private static bool SameNumber(double? legacy, ResolvedValue resolved) =>
        resolved.Source == ValueSource.BuiltIn
            ? legacy is null
            : legacy is { } value && Math.Abs(value - double.Parse(resolved.Value, System.Globalization.CultureInfo.InvariantCulture)) < 0.001;

    private static bool ToggleOnSeveralLevels(DocxDocument docx, ResolvedParagraph paragraph, ResolvedRun run, Func<Style, bool> sets)
    {
        var levels = 0;
        if (paragraph.Table?.TableStyleId is not null)
        {
            levels++;
        }

        if (docx.StyleChain(paragraph.StyleId).Any(sets))
        {
            levels++;
        }

        if (docx.StyleChain(run.CharacterStyleId).Any(sets))
        {
            levels++;
        }

        return levels > 1;
    }

    private static bool UsesThemeColor(DocxDocument docx, ResolvedRun run, ResolvedValue color)
    {
        var element = color.Source switch
        {
            ValueSource.Direct => run.Element.RunProperties?.Color,
            ValueSource.DocDefaults => docx.Main.StyleDefinitionsPart?.Styles?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.Color,
            ValueSource.ParagraphStyle or ValueSource.CharacterStyle when color.SourceStyleId is not null =>
                docx.StyleChain(color.SourceStyleId).LastOrDefault()?.StyleRunProperties?.Color,
            _ => null,
        };
        return element?.ThemeColor is not null;
    }
}
