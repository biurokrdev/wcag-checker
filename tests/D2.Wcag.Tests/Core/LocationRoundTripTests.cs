using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Core;

// Odwrotne wiązanie lokalizacji: klucz wystąpienia → element, mapa tekstu akapitu, sekcje, nagłówki i stopki.
public class LocationRoundTripTests
{
    public static IEnumerable<object[]> Documents() => SnapshotCases.AllDocuments();

    internal static DocxDocument Open(byte[] docx) => new(WordprocessingDocument.Open(new MemoryStream(docx), false));

    internal static DocxDocument Open(string name) => Open(SnapshotCases.Load(name));

    [Theory]
    [MemberData(nameof(Documents))]
    public void Resolve_ReturnsTheSameElement_ForEveryLocatedElementOfEveryStory(string name)
    {
        var docx = Open(name);
        using var package = docx.Package;
        var checkedElements = 0;

        foreach (var story in docx.Stories())
        {
            var elements = story.Root.Descendants().Where(e => e is Paragraph or Run or Table or TableCell or Drawing or Hyperlink);
            foreach (var element in elements)
            {
                var location = docx.Locate(element);
                if (location is null || location.Kind != KindOf(element))
                {
                    // Element bez własnego numeru (np. komórka w wierszu opakowanym w w:sdt) wskazuje przodka.
                    continue;
                }

                Assert.True(ReferenceEquals(docx.Resolve(location.Key), element), $"{name}: {location.Key} nie wraca do tego samego elementu");
                Assert.True(ReferenceEquals(docx.Resolve(location), element), $"{name}: {location.Key}");
                checkedElements++;
            }

            // Każdy akapit i każdy run w akapicie ma własny numer.
            Assert.All(story.Root.Descendants<Paragraph>(), p => Assert.Equal(LocationKind.Paragraph, docx.Locate(p)!.Kind));
            Assert.All(story.Root.Descendants<Paragraph>().SelectMany(p => p.Descendants<Run>()), r => Assert.Contains(docx.Locate(r)!.Kind, new[] { LocationKind.Run }));
        }

        Assert.True(checkedElements > 0);
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryIssueLocation_ResolvesOnAFreshCopy_ToTheSameKindOfElementAndText(string name)
    {
        var bytes = SnapshotCases.Load(name);
        var validator = new WcagDocxValidator();
        var profile = new WcagValidationProfile().WithStandards([.. validator.Catalog.All.Select(s => D2.Wcag.Standards.DocumentStandardExtensions.Key(s))]);
        var result = validator.Validate(bytes, profile);
        var first = Open(bytes);
        var second = Open(bytes);
        using var firstPackage = first.Package;
        using var secondPackage = second.Package;

        var locations = result.Issues.SelectMany(i => i.Locations).ToList();
        Assert.NotEmpty(locations);
        foreach (var location in locations)
        {
            if (location.Kind == LocationKind.Document)
            {
                Assert.Null(second.Resolve(location));
                continue;
            }

            var element = second.Resolve(location.Key);
            Assert.True(element is not null, $"{name}: {location.Key} nie wskazuje elementu");
            Assert.Equal(location.Kind, KindOf(element!));
            Assert.Equal(first.Resolve(location)!.InnerText, element!.InnerText);
            Assert.Equal(location.Key, second.Locate(element)!.Key);
        }
    }

    [Fact]
    public void Resolve_ReturnsNull_ForDocumentUnknownStoryOutOfRangeAndMalformedKey()
    {
        var docx = Open("clean");
        using var package = docx.Package;

        Assert.Null(docx.Resolve(IssueLocation.Document()));
        Assert.Null(docx.Resolve("body:Document:0"));
        Assert.Null(docx.Resolve("footnotes:Paragraph:0"));
        Assert.Null(docx.Resolve("header/7:Paragraph:0"));
        Assert.Null(docx.Resolve("body:Paragraph:9999"));
        Assert.Null(docx.Resolve("body:Run:0"));
        Assert.Null(docx.Resolve("body:TableCell:0"));
        Assert.Null(docx.Resolve("to nie jest klucz"));
        Assert.IsType<Paragraph>(docx.Resolve("body:Paragraph:0"));
        Assert.IsType<Paragraph>(docx.Resolve("header/0:Paragraph:1"));
        Assert.IsType<Run>(docx.Resolve("footer/0:Run:0:0"));
        Assert.IsType<TableCell>(docx.Resolve("body:TableCell:0:1"));
        Assert.IsType<Hyperlink>(docx.Resolve("body:Hyperlink:0"));
        Assert.IsType<Drawing>(docx.Resolve("header/0:Drawing:0"));
    }

    // Założenie z docs/contracts.md pkt 10: akapity z mc:Choice i mc:Fallback są liczone oba (osobne numery),
    // a run pola tekstowego należy do akapitu zagnieżdżonego, więc numery runów akapitu zewnętrznego mają lukę.
    [Fact]
    public void AlternateContent_CountsBothChoiceAndFallbackParagraphs_WithStableKeys()
    {
        var docx = Open("edge");
        using var package = docx.Package;

        var boxes = docx.Body.Descendants<Paragraph>().Where(p => p.InnerText == "Uwaga w polu tekstowym.").ToList();
        Assert.Equal(2, boxes.Count);
        Assert.Single(boxes, p => p.Ancestors<AlternateContentChoice>().Any());
        Assert.Single(boxes, p => p.Ancestors<AlternateContentFallback>().Any());

        var outer = docx.Body.Descendants<Paragraph>().Single(p => p.Elements<Run>().Any(r => r.InnerText == "Akapit z polem tekstowym: "));
        var outerIndex = docx.Locate(outer)!.Index;
        Assert.Equal([outerIndex + 1, outerIndex + 2], boxes.Select(p => docx.Locate(p)!.Index));

        // Runy akapitu zewnętrznego: 0 = tekst, 1 = run z polem; runy 2 i 3 (w polu) należą do akapitów zagnieżdżonych.
        Assert.Equal([$"body:Run:{outerIndex}:0", $"body:Run:{outerIndex}:1"], outer.Elements<Run>().Select(r => docx.Locate(r)!.Key));
        Assert.Equal([$"body:Run:{outerIndex + 1}:0", $"body:Run:{outerIndex + 2}:0"], boxes.Select(p => docx.Locate(p.Descendants<Run>().Single())!.Key));
        Assert.Null(docx.Resolve($"body:Run:{outerIndex}:2"));

        var map = docx.TextMap(outer);
        Assert.Equal("Akapit z polem tekstowym: ", map.Text);
        Assert.Equal([0, 1], map.Segments.Select(s => s.RunIndex));
        Assert.Equal("Uwaga w polu tekstowym.", docx.TextMap(boxes[0]).Text);
    }

    [Fact]
    public void TextMap_MapsTabsBreaksAndRuns_AndFindsRunsCoveringARange()
    {
        var docx = Open("edge");
        using var package = docx.Package;

        var amount = docx.Body.Descendants<Paragraph>().Single(p => p.InnerText.StartsWith("Kwota:", StringComparison.Ordinal));
        var amountMap = docx.TextMap(amount);
        Assert.Equal("Kwota:\t100 zł\npłatne do końca miesiąca.", amountMap.Text);
        Assert.Equal([(0, 0, 6), (1, 6, 32), (2, 38, 1)], amountMap.Segments.Select(s => (s.RunIndex, s.Start, s.Length)));
        Assert.Same(amountMap, docx.TextMap(amount));

        // Fraza rozbita na 3 runy: zakres kotwiczy się na granicach runów 1–3.
        var split = docx.Body.Descendants<Paragraph>().Single(p => p.InnerText.Contains("Example Company S. A.", StringComparison.Ordinal));
        var map = docx.TextMap(split);
        var start = map.Text.IndexOf("Example Company S. A.", StringComparison.Ordinal);
        Assert.Equal(28, start);
        Assert.Equal((1, 3), map.RunsCovering(new TextRange(start, "Example Company S. A.".Length)));
        Assert.Equal((2, 2), map.RunsCovering(new TextRange(map.Text.IndexOf("Company", StringComparison.Ordinal), 7)));
        Assert.Equal((0, 4), map.RunsCovering(new TextRange(0, map.Text.Length)));
        Assert.Equal((1, 1), map.RunsCovering(new TextRange(start, 0)));
        Assert.Null(map.RunsCovering(new TextRange(map.Text.Length, 0)));
        Assert.Null(map.RunsCovering(new TextRange(0, map.Text.Length + 1)));
        Assert.Null(map.RunsCovering(new TextRange(-1, 2)));
        Assert.All(map.Segments, s => Assert.True(ReferenceEquals(docx.Resolve($"body:Run:{docx.Locate(split)!.Index}:{s.RunIndex}"), s.Run)));

        // Run z odwołaniem do komentarza nie ma tekstu: segment o długości 0 nie bierze udziału w zakresach.
        var commented = docx.Body.Descendants<Paragraph>().Single(p => p.InnerText.Contains("istniejącym komentarzem", StringComparison.Ordinal));
        var commentedMap = docx.TextMap(commented);
        Assert.Equal("Ten akapit ma fragment z istniejącym komentarzem recenzenta.", commentedMap.Text);
        Assert.Equal([14, 34, 0, 12], commentedMap.Segments.Select(s => s.Length));
        Assert.Equal((1, 3), commentedMap.RunsCovering(new TextRange(14, 35)));
    }

    [Fact]
    public void Sections_DescribeParagraphRanges_TitlePage_AndFirstParagraph()
    {
        var docx = Open("edge");
        using var package = docx.Package;

        var sections = docx.Sections();

        Assert.Equal(2, sections.Count);
        Assert.Equal([0, 1], sections.Select(s => s.Index));
        Assert.True(sections[0].TitlePage);
        Assert.False(sections[1].TitlePage);
        Assert.Equal(0, sections[0].FirstParagraphIndex);
        var holder = docx.Body.Descendants<Paragraph>().Single(p => p.InnerText == "Ostatni akapit pierwszej sekcji.");
        Assert.Equal(docx.Locate(holder)!.Index, sections[0].LastParagraphIndex);
        Assert.Equal(sections[0].LastParagraphIndex + 1, sections[1].FirstParagraphIndex);
        Assert.Equal(docx.Body.Descendants<Paragraph>().Count() - 1, sections[1].LastParagraphIndex);
        Assert.Same(sections, docx.Sections());

        Assert.Equal("body:Paragraph:0", docx.LocateSection(0)!.Key);
        Assert.Equal("Druga sekcja", docx.LocateSection(1)!.Snippet);
        Assert.Null(docx.LocateSection(2));
        Assert.Null(docx.LocateSection(-1));

        var single = Open("clean");
        using var singlePackage = single.Package;
        var only = Assert.Single(single.Sections());
        Assert.Equal((0, single.Body.Descendants<Paragraph>().Count() - 1, false), (only.FirstParagraphIndex, only.LastParagraphIndex, only.TitlePage));
    }

    [Fact]
    public void StoryUsages_TellWhereHeadersAndFootersAreUsed_IncludingInheritance()
    {
        var docx = Open("edge");
        using var package = docx.Package;
        string Footer(string text) => docx.StoryKey(docx.Main.FooterParts.Single(f => f.Footer!.InnerText.Contains(text, StringComparison.Ordinal)));

        var firstDefault = Footer("Strona: 1 z 1. Przypadki brzegowe");
        var firstPage = Footer("Stopka pierwszej strony");
        var even = Footer("Stopka stron parzystych");
        var secondDefault = Footer("Sekcja 2.");

        Assert.Equal([new StoryUsage(firstDefault, 0, PageKind.Default, false, true)], docx.StoryUsages(firstDefault));
        // Pierwsza strona: aktywna w sekcji 1 (w:titlePg), odziedziczona i nieaktywna w sekcji 2.
        Assert.Equal(
            [new StoryUsage(firstPage, 0, PageKind.First, false, true), new StoryUsage(firstPage, 1, PageKind.First, true, false)],
            docx.StoryUsages(firstPage));
        // Strony parzyste: w:evenAndOddHeaders w ustawieniach, sekcja 2 dziedziczy.
        Assert.Equal(
            [new StoryUsage(even, 0, PageKind.Even, false, true), new StoryUsage(even, 1, PageKind.Even, true, true)],
            docx.StoryUsages(even));
        Assert.Equal([new StoryUsage(secondDefault, 1, PageKind.Default, false, true)], docx.StoryUsages(secondDefault));
        // Nagłówek z sekcji 1 jest w sekcji 2 odziedziczony.
        Assert.Equal(
            [new StoryUsage("header/0", 0, PageKind.Default, false, true), new StoryUsage("header/0", 1, PageKind.Default, true, true)],
            docx.StoryUsages("header/0"));
        Assert.Empty(docx.StoryUsages("body"));
        Assert.Empty(docx.StoryUsages("footnotes"));
        Assert.Empty(docx.StoryUsages("footer/99"));

        Assert.Equal("stopki (sekcja 1, strony: nieparzyste)", docx.DescribeStory(firstDefault));
        Assert.Equal("stopki (sekcja 1, strona: pierwsza)", docx.DescribeStory(firstPage));
        Assert.Equal("stopki (sekcja 1, strony: parzyste)", docx.DescribeStory(even));
        Assert.Equal("stopki (sekcja 2, strony: nieparzyste)", docx.DescribeStory(secondDefault));
        Assert.Equal("nagłówka strony (sekcja 1, strony: nieparzyste)", docx.DescribeStory("header/0"));
        Assert.Equal("treści dokumentu", docx.DescribeStory("body"));
        Assert.Equal("przypisów dolnych", docx.DescribeStory("footnotes"));
        Assert.Equal("przypisów końcowych", docx.DescribeStory("endnotes"));
        Assert.Equal("stopki (nieużywanej w żadnej sekcji)", docx.DescribeStory("footer/99"));

        var clean = Open("clean");
        using var cleanPackage = clean.Package;
        Assert.Equal("stopki (sekcja 1, strony: wszystkie)", clean.DescribeStory("footer/0"));
    }

    [Fact]
    public void OrphanedFooter_AndInactiveFirstPageFooter_AreReported()
    {
        var bytes = new DocxBuilder().Heading(1, "A").Add(main =>
        {
            // Stopka pierwszej strony bez w:titlePg (nieaktywna) i część stopki, której nie wskazuje żadna sekcja.
            var orphan = main.AddNewPart<FooterPart>();
            orphan.Footer = new Footer(new Paragraph(new Run(new Text("Osierocona"))));
            return new Paragraph(new Run(new Text("Treść.")));
        }).Build();
        var docx = Open(bytes);
        using var package = docx.Package;
        var orphanKey = docx.StoryKey(docx.Main.FooterParts.Single(f => f.Footer!.InnerText == "Osierocona"));
        var usedKey = docx.StoryKey(docx.Main.FooterParts.Single(f => f.Footer!.InnerText != "Osierocona"));

        Assert.Empty(docx.StoryUsages(orphanKey));
        Assert.Equal("stopki (nieużywanej w żadnej sekcji)", docx.DescribeStory(orphanKey));
        Assert.Equal([new StoryUsage(usedKey, 0, PageKind.Default, false, true)], docx.StoryUsages(usedKey));
    }

    [Fact]
    public void NoteReferenceParagraph_FindsTheBodyParagraphWithTheFootnoteReference()
    {
        var docx = Open("edge");
        using var package = docx.Package;
        var note = docx.Main.FootnotesPart!.Footnotes!.Descendants<Paragraph>().Single(p => p.InnerText.Contains("Treść przypisu", StringComparison.Ordinal));
        var noteLocation = docx.Locate(note)!;
        var runLocation = docx.Locate(note.Descendants<Run>().Last())!;

        Assert.Equal("footnotes", noteLocation.Story);
        var paragraph = docx.NoteReferenceParagraph(noteLocation);
        Assert.NotNull(paragraph);
        Assert.Equal("Zdanie z przypisem dolnym.", docx.TextMap(paragraph!).Text);
        Assert.Same(paragraph, docx.NoteReferenceParagraph(runLocation));
        // Separator przypisów nie ma odwołania w treści; wystąpienie spoza przypisów też nie.
        Assert.Null(docx.NoteReferenceParagraph(new IssueLocation(LocationKind.Paragraph, "footnotes", 0, null, "")));
        Assert.Null(docx.NoteReferenceParagraph(docx.Locate(paragraph!)!));
    }

    private static LocationKind KindOf(OpenXmlElement element) => element switch
    {
        Paragraph => LocationKind.Paragraph,
        Run => LocationKind.Run,
        Table => LocationKind.Table,
        TableCell => LocationKind.TableCell,
        Drawing => LocationKind.Drawing,
        Hyperlink => LocationKind.Hyperlink,
        _ => LocationKind.Document,
    };
}
