using System.Diagnostics;
using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using D2.Wcag.Structure;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Tests.Hierarchy;

// Drzewo treści (ContentStructure.Build): nagłówki z treścią pod nimi, zwinięte akapity, listy z poziomami, tabele.
public class ContentStructureTests
{
    private static readonly WcagValidationProfile Hierarchy = new WcagValidationProfile().WithStandards("hierarchia/1.0");

    private static IReadOnlyList<StructureNode> Tree(byte[] docx) => new WcagDocxValidator().Validate(docx, Hierarchy).Structure;

    private static byte[] Document(Action<Body> content) => new FixtureDocx { Content = (_, _, body) => content(body) }.Build();

    private static IEnumerable<StructureNode> Flat(IEnumerable<StructureNode> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flat(n.Children)));

    public static IEnumerable<object[]> Documents() => SnapshotCases.AllDocuments();

    [Theory]
    [MemberData(nameof(Documents))]
    public void Structure_MatchesSnapshot(string document)
    {
        var tree = Tree(SnapshotCases.Load(document));

        Snapshot.Match($"lib/{document}.structure.json", Snapshot.Json(tree));
    }

    [Fact]
    public void Structure_DoesNotDependOnSelectedStandards()
    {
        var docx = FixtureCatalog.Build("hierarchy-broken");

        var withWcag = new WcagDocxValidator().Validate(docx, new WcagValidationProfile().WithStandards("wcag/2.1")).Structure;

        Assert.Equal(Snapshot.Json(Tree(docx)), Snapshot.Json(withWcag));
        Assert.NotEmpty(withWcag);
    }

    [Fact]
    public void Structure_IsDeterministic()
    {
        var docx = FixtureCatalog.Build("hierarchy-broken");

        Assert.Equal(Snapshot.Json(Tree(docx)), Snapshot.Json(Tree(docx)));
    }

    [Fact]
    public void Clean_NestsHeadingsByLevel_WithContentUnderThem()
    {
        var tree = Tree(FixtureCatalog.Build("clean"));

        var title = Assert.Single(tree);
        Assert.Equal((StructureNodeKind.Heading, 1, "Nagłówek 1"), (title.Kind, title.Level, title.StyleName));
        Assert.Equal("body:Paragraph:0", title.Key);

        Assert.Equal(StructureNodeKind.Paragraphs, title.Children[0].Kind);
        Assert.Equal(2, title.Children[0].Count);
        Assert.StartsWith("To jest wzorcowy szablon pisma", title.Children[0].Text);
        Assert.Equal("Normalny", title.Children[0].StyleName);

        var sections = title.Children.Where(n => n.Kind == StructureNodeKind.Heading).ToList();
        Assert.Equal(["Dane umowy", "Wykaz opłat", "Warunki promocji"], sections.Select(s => s.Text));
        Assert.All(sections, s => Assert.Equal(2, s.Level));

        // H3 pod H2, H4 pod H3, H5 pod H4.
        var documents = Assert.Single(sections[2].Children, n => n.Kind == StructureNodeKind.Heading);
        Assert.Equal(("Dokumenty do pobrania", 3), (documents.Text, documents.Level));
        var contact = Assert.Single(documents.Children, n => n.Kind == StructureNodeKind.Heading);
        Assert.Equal(("Kontakt", 4), (contact.Text, contact.Level));
        Assert.Equal(("Podpis", 5), (contact.Children.Last().Text, contact.Children.Last().Level));
    }

    [Fact]
    public void Clean_ShowsTablesAndListsAsNodes()
    {
        var tree = Tree(FixtureCatalog.Build("clean"));
        var all = Flat(tree).ToList();

        var table = Assert.Single(all, n => n.Kind == StructureNodeKind.Table);
        Assert.Equal(("Tabela 4 × 2", "4 × 2", 4, "body:Table:0"), (table.Text, table.Label, table.Count, table.Key));
        Assert.Equal("Tabela standardowa", table.StyleName);
        var cells = Assert.Single(table.Children);
        Assert.Equal((StructureNodeKind.Paragraphs, 8), (cells.Kind, cells.Count));

        var lists = all.Where(n => n.Kind == StructureNodeKind.List).ToList();
        Assert.Equal(["Lista punktowana", "Lista numerowana"], lists.Select(l => l.Text));
        Assert.All(lists, l => Assert.Equal(3, l.Count));
        Assert.Equal(["•", "•", "•"], lists[0].Children.Select(i => i.Label));
        Assert.Equal(["1)", "2)", "3)"], lists[1].Children.Select(i => i.Label));
        Assert.All(lists.SelectMany(l => l.Children), i => Assert.Equal((StructureNodeKind.ListItem, 0, "Akapit z listą"), (i.Kind, i.Level, i.StyleName)));
    }

    [Fact]
    public void Lists_NestItemsByLevel_AndKeepSkippedLevels()
    {
        var tree = Tree(FixtureCatalog.Build("hierarchy-broken"));

        var list = Flat(tree).First(n => n.Kind == StructureNodeKind.List && n.Children[0].Text.StartsWith("pozycja poziomu pierwszego", StringComparison.Ordinal));

        Assert.Equal(4, list.Count);
        Assert.Equal(2, list.Children.Count);
        Assert.Equal([0, 0], list.Children.Select(i => i.Level));
        Assert.Equal(1, Assert.Single(list.Children[0].Children).Level);
        var skipped = Assert.Single(list.Children[1].Children);
        Assert.Equal((2, "pozycja od razu na poziomie trzecim."), (skipped.Level, skipped.Text));
    }

    [Fact]
    public void Headings_CarryTheirNumber_FromMultilevelNumbering()
    {
        var tree = Tree(FixtureCatalog.Build("hierarchy-broken"));

        var numbered = Flat(tree).Where(n => n.Kind == StructureNodeKind.Heading && n.Label is not null).ToList();

        Assert.Equal(
            [("Zasady numeracji", "1."), ("Numer zgodny z poziomem", "1.1."), ("Numer z niewłaściwego poziomu", "1.1.1.")],
            numbered.Select(n => (n.Text, n.Label!)));
    }

    [Fact]
    public void Tables_KeepTheirContentAsChildren_IncludingHeadingsInCells()
    {
        var tree = Tree(FixtureCatalog.Build("hierarchy-broken"));

        var table = Assert.Single(Flat(tree), n => n.Kind == StructureNodeKind.Table);

        Assert.Equal("Tabela 3 × 2", table.Text);
        var heading = Assert.Single(table.Children, n => n.Kind == StructureNodeKind.Heading);
        Assert.Equal(("Nagłówek w komórce", 3), (heading.Text, heading.Level));
    }

    [Fact]
    public void DocumentWithoutHeadings_GivesFlatContent()
    {
        var tree = Tree(Document(body =>
        {
            body.Append(Para("Pierwszy akapit pisma."));
            body.Append(Para("Drugi akapit pisma."));
            body.Append(new Paragraph());
            body.Append(Para("Trzeci akapit pisma."));
            body.Append(ListItem(BulletNumId, "pozycja listy"));
            body.Append(StandardTable("Tytuł", "Opis", [5000, 5000], ["A", "B"], ["1", "2"]));
        }));

        Assert.Equal([StructureNodeKind.Paragraphs, StructureNodeKind.List, StructureNodeKind.Table], tree.Select(n => n.Kind));
        Assert.DoesNotContain(Flat(tree), n => n.Kind == StructureNodeKind.Heading);
        // Pusty akapit nie tworzy węzła i nie przerywa grupy.
        Assert.Equal((3, "Pierwszy akapit pisma.", "body:Paragraph:0"), (tree[0].Count, tree[0].Text, tree[0].Key));
        Assert.Equal("body:Paragraph:4", tree[1].Key);
    }

    [Fact]
    public void EmptyDocument_GivesEmptyTree()
    {
        Assert.Empty(Tree(Document(_ => { })));
        Assert.Empty(Tree(Document(body => body.Append(new Paragraph(), new Paragraph(new Run(new Text("   ") { Space = SpaceProcessingModeValues.Preserve }))))));
        Assert.Empty(Tree(new DocxBuilder().Build()));
    }

    [Fact]
    public void Paragraphs_SplitIntoGroups_WhenTheStyleChanges()
    {
        var tree = Tree(FixtureCatalog.Build("hierarchy-broken"));

        Assert.Equal(
            [(StructureNodeKind.Paragraphs, "Tytuł", 1), (StructureNodeKind.Paragraphs, "Normalny", 1)],
            tree.Take(2).Select(n => (n.Kind, n.StyleName!, n.Count)));
    }

    [Fact]
    public void ContentControls_AreTransparent_AndFallbackCopiesAreSkipped()
    {
        var tree = Tree(Document(body =>
        {
            body.Append(new SdtBlock(new SdtContentBlock(Heading(1, "Tytuł w formancie"), Para("Treść w formancie."))));
            body.Append(new AlternateContent(
                new AlternateContentChoice(Para("Treść wybrana.")) { Requires = "w14" },
                new AlternateContentFallback(Para("Kopia zapasowa."))));
        }));

        var heading = Assert.Single(tree);
        Assert.Equal("Tytuł w formancie", heading.Text);
        var content = Assert.Single(heading.Children);
        Assert.Equal((2, "Treść w formancie."), (content.Count, content.Text));
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryNodeKey_PointsAtAnElementOfTheDocument(string name)
    {
        var docx = SnapshotCases.Load(name);
        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var document = new DocxDocument(package);

        var nodes = Flat(ContentStructure.Build(document)).ToList();

        Assert.NotEmpty(nodes);
        Assert.All(nodes, node =>
        {
            Assert.NotNull(node.Key);
            var element = document.Resolve(node.Key!);
            Assert.True(node.Kind == StructureNodeKind.Table ? element is Table : element is Paragraph, $"{node.Kind} {node.Key}");
        });

        // Klucze akapitowe rosną w kolejności drzewa – na tym opiera się przypisanie zgłoszeń do węzłów w panelu.
        // Lista ma klucz swojej pierwszej pozycji; poza tym każdy akapit ma najwyżej jeden węzeł.
        var paragraphs = nodes.Where(n => n.Kind != StructureNodeKind.Table).Select(n => int.Parse(n.Key!.Split(':')[2])).ToList();
        Assert.Equal(paragraphs.OrderBy(i => i), paragraphs);
        var own = nodes.Where(n => n.Kind is not (StructureNodeKind.Table or StructureNodeKind.List)).Select(n => n.Key).ToList();
        Assert.Equal(own.Count, own.Distinct().Count());
    }

    [Fact]
    public void LargeDocument_BuildsInLinearTime()
    {
        // Rozgrzewka (JIT, wyrażenia regularne), potem pomiar: 5000 akapitów nie może kosztować wielokrotnie więcej
        // niż wynikałoby z proporcji do 1250 (algorytm kwadratowy dałby stosunek ok. 16).
        Measure(LargeDocument(400));
        var small = Enumerable.Range(0, 3).Min(_ => Measure(LargeDocument(1250)));
        var large = Enumerable.Range(0, 3).Min(_ => Measure(LargeDocument(5000)));

        Assert.True(large < TimeSpan.FromSeconds(5), $"5000 akapitów: {large.TotalMilliseconds:0} ms");
        Assert.True(large.TotalMilliseconds < Math.Max(small.TotalMilliseconds, 5) * 10, $"1250 akapitów: {small.TotalMilliseconds:0} ms, 5000 akapitów: {large.TotalMilliseconds:0} ms");
    }

    [Fact]
    public void LargeDocument_KeepsEveryParagraphInTheTree()
    {
        var docx = LargeDocument(5000);

        var result = new WcagDocxValidator().Validate(docx, Hierarchy);

        var nodes = Flat(result.Structure).ToList();
        var paragraphs = nodes.Where(n => n.Kind is StructureNodeKind.Heading or StructureNodeKind.ListItem).Count()
            + nodes.Where(n => n.Kind == StructureNodeKind.Paragraphs).Sum(n => n.Count);
        Assert.Equal(5000, paragraphs);
        Assert.Empty(result.Issues);
    }

    // Dokument o poprawnej strukturze: H1, a pod nim sekcje H2 z H3, akapitami i listą dwupoziomową.
    private static byte[] LargeDocument(int paragraphs) => Document(body =>
    {
        body.Append(Heading(1, "Duży dokument"));
        for (var i = 1; i < paragraphs; i++)
        {
            body.Append((i % 25) switch
            {
                1 => Heading(2, $"Sekcja {i}"),
                8 => Heading(3, $"Podsekcja {i}"),
                12 or 13 or 15 => ListItem(BulletNumId, $"pozycja {i},"),
                14 => ListItem(BulletNumId, $"pozycja zagnieżdżona {i},", 1),
                _ => Para($"Akapit numer {i} z treścią, która opisuje przykładowe postanowienie regulaminu."),
            });
        }
    });

    private static TimeSpan Measure(byte[] docx)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var document = new DocxDocument(package);
        var standard = new D2.Wcag.Standards.HierarchyStandard();

        var watch = Stopwatch.StartNew();
        var tree = ContentStructure.Build(document);
        var issues = standard.Rules.SelectMany(r => r.Check(document)).Count();
        watch.Stop();

        Assert.NotEmpty(tree);
        Assert.Equal(0, issues);
        return watch.Elapsed;
    }
}
