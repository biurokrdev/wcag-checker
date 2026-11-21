using System.IO.Compression;
using System.Text;
using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Tests.CommentExport.CommentTestKit;

namespace D2.Wcag.Tests.CommentExport;

// Zapis komentarzy na zgłoszeniach zbudowanych ręcznie (także z polami nowego kontraktu: Status, Title, Fix, Range,
// Scope) – treść komentarza, kotwice w przypadkach brzegowych i komunikaty błędów.
public class CommentWriterTests
{
    private static readonly WcagCommentWriter Writer = new();

    private static readonly byte[] Edge = FixtureCatalog.Build("edge");

    private static WcagIssue Issue(string code, IssueSeverity severity, string message, params IssueLocation[] locations) =>
        new WcagIssue(code, severity, IssueCategory.Text, message).At(locations);

    private static IReadOnlyList<ReadComment> Annotate(byte[] docx, params WcagIssue[] issues) => Added(Writer.Annotate(docx, issues, Options).Content);

    private static IssueLocation BodyParagraph(byte[] docx, string text) =>
        Locate(docx, d => d.Body.Descendants<Paragraph>().First(p => p.InnerText.Contains(text)));

    // ------------------------------------------------------------ treść komentarza

    [Fact]
    public void Comment_HasCodeTitleStatus_Problem_AndFix()
    {
        var issue = Issue("STY-DIRECT-REDUNDANT", IssueSeverity.Error, "Interlinia 1,0; oczekiwano 1,5.", BodyParagraph(Edge, "Ostatni akapit dokumentu")) with
        {
            Status = SpecStatus.Required,
            Title = "Zbędne formatowanie bezpośrednie",
            Fix = "Zaznacz akapit i naciśnij Ctrl+Q.",
            Details = [new IssueDetail(FormatProps.LineSpacing, "12", "18", "formatowanie bezpośrednie")],
        };

        var comment = Assert.Single(Annotate(Edge, issue));

        Assert.Equal(
            [
                "[STY-DIRECT-REDUNDANT] Zbędne formatowanie bezpośrednie  ·  WYMAGANE → błąd",
                "Problem: Interlinia 1,0; oczekiwano 1,5.",
                "Jak naprawić: Zaznacz akapit i naciśnij Ctrl+Q.",
            ],
            comment.Lines);
        Assert.Equal("Ostatni akapit dokumentu.", comment.AnchorText);
        Assert.Equal("Audytor szablonów", comment.Author);
        Assert.Equal("AS", comment.Initials);
    }

    [Theory]
    [InlineData(SpecStatus.Required, IssueSeverity.Error, "WYMAGANE → błąd")]
    [InlineData(SpecStatus.Recommended, IssueSeverity.Warning, "REKOMENDOWANE → ostrzeżenie")]
    [InlineData(SpecStatus.Conditional, IssueSeverity.Info, "WARUNKOWE → informacja")]
    [InlineData(SpecStatus.ToVerify, IssueSeverity.Info, "DO WERYFIKACJI → informacja")]
    public void Comment_ShowsSpecStatusWithSeverity(SpecStatus status, IssueSeverity severity, string expected)
    {
        var issue = Issue("X-1", severity, "Opis.", BodyParagraph(Edge, "Ostatni akapit dokumentu")) with { Status = status, Title = "Tytuł" };

        var comment = Assert.Single(Annotate(Edge, issue));

        Assert.Equal($"[X-1] Tytuł  ·  {expected}", comment.Lines[0]);
    }

    [Fact]
    public void Comment_WithoutStatusTitleAndFix_ShowsSeverityAndDictionaryTitle_AndSkipsFixLine()
    {
        var issue = Issue(IssueCodes.JustifiedText, IssueSeverity.Warning, "Akapit jest wyjustowany; wyrównaj do lewej.", BodyParagraph(Edge, "Ostatni akapit dokumentu"));

        var comment = Assert.Single(Annotate(Edge, issue));

        Assert.Equal(
            [$"[JUSTIFIED_TEXT] {IssueLabels.Of(IssueCodes.JustifiedText)}  ·  ostrzeżenie", "Problem: Akapit jest wyjustowany; wyrównaj do lewej."],
            comment.Lines);
    }

    [Fact]
    public void Comment_EscapesMarkup_DropsControlCharacters_AndSplitsMultilineFix()
    {
        var issue = Issue("X-2", IssueSeverity.Info, "Tekst <b>&amp; \"cudzysłów\" \u0001 koniec", BodyParagraph(Edge, "Ostatni akapit dokumentu")) with
        {
            Title = "A < B & C",
            Fix = "1. Otwórz okno Style.\r\n2. Wybierz „Normalny”.",
        };

        var annotated = Writer.Annotate(Edge, [issue], Options);
        var comment = Assert.Single(Added(annotated.Content));

        Assert.Equal(
            [
                "[X-2] A < B & C  ·  informacja",
                "Problem: Tekst <b>&amp; \"cudzysłów\"   koniec",
                "Jak naprawić: 1. Otwórz okno Style.",
                "2. Wybierz „Normalny”.",
            ],
            comment.Lines);
        Assert.Empty(SchemaErrors(annotated.Content).Except(SchemaErrors(Edge)));
    }

    // ------------------------------------------------------------ kotwice

    [Fact]
    public void RangeAcrossRuns_IsAnchoredOnRunBoundaries_WithoutSplittingRuns()
    {
        var paragraph = BodyParagraph(Edge, "Administratorem danych jest");
        var text = "Administratorem danych jest Example Company S. A. z siedzibą w Warszawie.";
        var exact = paragraph with { Range = new TextRange(text.IndexOf("Example", StringComparison.Ordinal), "Example Company S. A.".Length) };
        var partial = paragraph with { Range = new TextRange(text.IndexOf("ompany", StringComparison.Ordinal), "ompany S.".Length) };
        var outside = paragraph with { Range = new TextRange(500, 3) };

        var annotated = Writer.Annotate(
            Edge,
            [Issue("R-EXACT", IssueSeverity.Info, "a", exact), Issue("R-PART", IssueSeverity.Info, "b", partial), Issue("R-OUT", IssueSeverity.Info, "c", outside)],
            Options);
        var comments = Added(annotated.Content).ToDictionary(c => c.Lines[0].Split(']')[0][1..]);

        Assert.Equal("Example Company S. A.", comments["R-EXACT"].AnchorText);
        Assert.Equal(" Company S. A.", comments["R-PART"].AnchorText);
        // Zakres poza tekstem akapitu: komentarz na całym akapicie.
        Assert.Equal(text, comments["R-OUT"].AnchorText);
        Assert.Equal(MainXml(Edge), MainXmlWithoutComments(annotated.Content));
    }

    [Fact]
    public void SeveralIssuesInOnePlace_GetSeparateComments_OrderedBySeverityThenCode()
    {
        var run = Locate(Edge, d => d.Body.Descendants<Run>().First(r => r.InnerText == "rozjaśniony akcent"));

        var annotated = Writer.Annotate(
            Edge,
            [
                Issue("B-WARN", IssueSeverity.Warning, "2", run),
                Issue("Z-INFO", IssueSeverity.Info, "4", run),
                Issue("C-ERR", IssueSeverity.Error, "1", run),
                Issue("A-WARN", IssueSeverity.Warning, "3", run),
                Issue("A-ERR", IssueSeverity.Error, "0", run),
            ],
            Options);
        var comments = Added(annotated.Content);

        Assert.Equal(["A-ERR", "C-ERR", "A-WARN", "B-WARN", "Z-INFO"], comments.Select(c => c.Lines[0].Split(']')[0][1..]));
        Assert.Equal([1, 2, 3, 4, 5], comments.Select(c => c.Id));
        Assert.All(comments, c => Assert.Equal("rozjaśniony akcent", c.AnchorText));

        // Układ znaczników: wszystkie początki przed runem, potem pary „koniec + odwołanie” w kolejności komentarzy.
        using var package = WordprocessingDocument.Open(new MemoryStream(annotated.Content), false);
        var target = package.MainDocumentPart!.Document.Body!.Descendants<Run>().First(r => r.InnerText == "rozjaśniony akcent");
        var siblings = target.Parent!.ChildElements.ToList();
        var index = siblings.IndexOf(target);
        Assert.Equal(["1", "2", "3", "4", "5"], siblings.Skip(index - 5).Take(5).Cast<CommentRangeStart>().Select(m => m.Id!.Value));
        var after = siblings.Skip(index + 1).Take(10).ToList();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal((i + 1).ToString(), Assert.IsType<CommentRangeEnd>(after[2 * i]).Id!.Value);
            Assert.Equal((i + 1).ToString(), Assert.IsType<Run>(after[2 * i + 1]).GetFirstChild<CommentReference>()!.Id!.Value);
        }
    }

    [Fact]
    public void FooterFindings_AreAnchoredOnFirstParagraphOfTheirSection_WithStoryPrefix()
    {
        var firstPage = Locate(Edge, d => StoryRoot(d, "footer/1").Descendants<Paragraph>().First());
        var secondSection = Locate(Edge, d => StoryRoot(d, "footer/3").Descendants<Run>().First());
        var header = Locate(Edge, d => StoryRoot(d, "header/0").Descendants<Paragraph>().Last());

        var comments = Annotate(
            Edge,
            Issue("F-FIRST", IssueSeverity.Warning, "Stopka pierwszej strony.", firstPage),
            Issue("F-SECOND", IssueSeverity.Warning, "Stopka drugiej sekcji.", secondSection),
            Issue("H-ALL", IssueSeverity.Warning, "Nagłówek.", header));
        var byCode = comments.ToDictionary(c => c.Lines[1].Split(']')[0][1..]);

        Assert.Equal("Dotyczy stopki (sekcja 1, strona: pierwsza): Stopka pierwszej strony", byCode["F-FIRST"].Lines[0]);
        Assert.Equal("Przypadki brzegowe", byCode["F-FIRST"].AnchorText);
        Assert.Equal(0, byCode["F-FIRST"].Paragraph);

        Assert.StartsWith("Dotyczy stopki (sekcja 2, strony: nieparzyste): ", byCode["F-SECOND"].Lines[0]);
        Assert.Equal("Druga sekcja", byCode["F-SECOND"].AnchorText);

        Assert.Equal("Dotyczy nagłówka strony (sekcja 1, strony: nieparzyste): WZÓR", byCode["H-ALL"].Lines[0]);
        Assert.Equal(0, byCode["H-ALL"].Paragraph);
        Assert.All(comments, c => Assert.Equal(3, c.Lines.Count));
    }

    [Fact]
    public void FootnoteFinding_IsAnchoredOnParagraphWithTheReference()
    {
        var note = Locate(Edge, d => StoryRoot(d, "footnotes").Descendants<Paragraph>().First(p => p.InnerText.Contains("Treść przypisu")));

        var comment = Assert.Single(Annotate(Edge, Issue("N-1", IssueSeverity.Info, "Przypis.", note)));

        Assert.StartsWith("Dotyczy przypisów dolnych: ", comment.Lines[0]);
        Assert.Equal("Zdanie z przypisem dolnym.", comment.AnchorText);
    }

    [Fact]
    public void TextBoxFindings_InChoiceAndFallback_AreAnchoredOnTheParagraphHoldingTheShape()
    {
        IssueLocation Inner(int which) => Locate(Edge, d => d.Body.Descendants<Paragraph>().Where(p => p.InnerText == "Uwaga w polu tekstowym.").ElementAt(which));
        var innerRun = Locate(Edge, d => d.Body.Descendants<Run>().First(r => r.InnerText == "Uwaga w polu tekstowym." && r.Ancestors().Any(a => a.LocalName == "txbxContent")));

        var annotated = Writer.Annotate(
            Edge,
            [Issue("T-CHOICE", IssueSeverity.Info, "a", Inner(0)), Issue("T-FALLBACK", IssueSeverity.Info, "b", Inner(1)), Issue("T-RUN", IssueSeverity.Info, "c", innerRun)],
            Options);
        var comments = Added(annotated.Content);

        Assert.Equal(3, comments.Count);
        Assert.All(comments, c =>
        {
            Assert.Equal("Dotyczy pola tekstowego zakotwiczonego w tym akapicie (Word nie pozwala komentować wnętrza pola tekstowego): Uwaga w polu tekstowym.", c.Lines[0]);
            Assert.Equal("Akapit z polem tekstowym: Uwaga w polu tekstowym.", c.AnchorText);
        });

        using var package = WordprocessingDocument.Open(new MemoryStream(annotated.Content), false);
        var body = package.MainDocumentPart!.Document.Body!;
        Assert.DoesNotContain(body.Descendants<AlternateContent>().SelectMany(a => a.Descendants()), e => e is CommentRangeStart or CommentRangeEnd or CommentReference);
        Assert.Equal(MainXml(Edge), MainXmlWithoutComments(annotated.Content));
    }

    [Fact]
    public void Table_Cell_Hyperlink_AndDrawing_GetReasonableAnchors()
    {
        var table = Locate(Edge, d => d.Body.Descendants<Table>().First());
        var cell = Locate(Edge, d => d.Body.Descendants<TableCell>().First(c => c.InnerText == "Decyzja"));
        var link = Locate(Edge, d => d.Body.Descendants<Hyperlink>().First());
        var linkRun = Locate(Edge, d => d.Body.Descendants<Hyperlink>().First().Descendants<Run>().First());
        var drawing = Locate(Edge, d => d.Body.Descendants<Drawing>().First());

        var annotated = Writer.Annotate(
            Edge,
            [
                Issue("K-TABLE", IssueSeverity.Info, "a", table),
                Issue("K-CELL", IssueSeverity.Info, "b", cell),
                Issue("K-LINK", IssueSeverity.Info, "c", link),
                Issue("K-LINKRUN", IssueSeverity.Info, "d", linkRun),
                Issue("K-DRAWING", IssueSeverity.Info, "e", drawing),
            ],
            Options);
        var comments = Added(annotated.Content);
        ReadComment Of(string code) => comments.Single(c => c.Lines.Any(l => l.StartsWith($"[{code}]", StringComparison.Ordinal)));

        Assert.Equal("Dotyczy całej tabeli (komentarz w jej pierwszej komórce).", Of("K-TABLE").Lines[0]);
        Assert.Equal("Etap", Of("K-TABLE").AnchorText);
        Assert.Equal("Decyzja", Of("K-CELL").AnchorText);
        Assert.Equal("Regulamin (PDF)", Of("K-LINK").AnchorText);
        Assert.Equal("Regulamin (PDF)", Of("K-LINKRUN").AnchorText);
        // Rysunek (kształt pola tekstowego): kotwicą jest run, w którym leży.
        Assert.Equal("Uwaga w polu tekstowym.", Of("K-DRAWING").AnchorText);
        Assert.Equal(2, Of("K-DRAWING").Lines.Count);

        Assert.Equal(MainXml(Edge), MainXmlWithoutComments(annotated.Content));
        Assert.Empty(SchemaErrors(annotated.Content).Except(SchemaErrors(Edge)));
    }

    [Fact]
    public void SectionScope_AddsNote()
    {
        var first = Locate(Edge, d => d.Resolve(d.LocateSection(1)!)!);

        var comment = Assert.Single(Annotate(Edge, Issue("S-1", IssueSeverity.Error, "Marginesy sekcji.", first) with { Scope = IssueScope.Section }));

        Assert.Equal("Dotyczy całej sekcji, która zaczyna się w tym miejscu.", comment.Lines[0]);
        Assert.Equal("Druga sekcja", comment.AnchorText);
    }

    // ------------------------------------------------------------ komentarz zbiorczy i brak zgłoszeń

    [Fact]
    public void IssuesWithoutPlaceInContent_GoToOneCollectiveComment_GroupedAndOrdered()
    {
        var annotated = Writer.Annotate(
            Edge,
            [
                Issue("STY-UNUSED", IssueSeverity.Warning, "Styl „Stary” nie jest używany.") with { Scope = IssueScope.Styles, Title = "Nieużywany styl", Fix = "Usuń styl." },
                Issue(IssueCodes.TitleMissing, IssueSeverity.Error, "Brak tytułu w metadanych."),
                Issue(IssueCodes.AuthorMissing, IssueSeverity.Warning, "Brak autora.", IssueLocation.Document()),
                Issue("STY-BROKEN", IssueSeverity.Error, "Styl „Zły” ma złą czcionkę.") with { Scope = IssueScope.Styles, Status = SpecStatus.Required, Title = "Zły styl" },
                Issue("F-ORPHAN", IssueSeverity.Info, "Stopka bez sekcji.", new IssueLocation(LocationKind.Paragraph, "footer/9", 0, null, "osierocona")),
                Issue("P-GONE", IssueSeverity.Info, "Akapit, którego nie ma.", new IssueLocation(LocationKind.Paragraph, IssueLocation.BodyStory, 9999, null, "zniknął")),
                Issue("P-OK", IssueSeverity.Info, "Zwykłe wystąpienie.", BodyParagraph(Edge, "Ostatni akapit dokumentu")),
            ],
            Options);

        Assert.Equal(6, annotated.CollectedIssues);
        Assert.Equal([CommentKind.Collective, CommentKind.Occurrence], annotated.Comments.Select(c => c.Kind));
        var comments = Added(annotated.Content);
        Assert.Equal(2, comments.Count);
        var collective = comments[0];
        Assert.Equal(0, collective.Paragraph);
        Assert.Equal("Przypadki brzegowe", collective.AnchorText);
        Assert.Equal(
            [
                "Uwagi bez wskazanego miejsca w treści (6 uwag)",
                "Dokument i metadane",
                $"[TITLE_MISSING] {IssueLabels.Of(IssueCodes.TitleMissing)}  ·  błąd",
                "Problem: Brak tytułu w metadanych.",
                $"[AUTHOR_MISSING] {IssueLabels.Of(IssueCodes.AuthorMissing)}  ·  ostrzeżenie",
                "Problem: Brak autora.",
                "Style",
                "[STY-BROKEN] Zły styl  ·  WYMAGANE → błąd",
                "Problem: Styl „Zły” ma złą czcionkę.",
                "[STY-UNUSED] Nieużywany styl  ·  ostrzeżenie",
                "Problem: Styl „Stary” nie jest używany.",
                "Jak naprawić: Usuń styl.",
                "Nagłówki i stopki",
                $"[F-ORPHAN] {IssueLabels.Of("F-ORPHAN")}  ·  informacja",
                "Problem: Stopka bez sekcji.",
                "Dotyczy stopki (nieużywanej w żadnej sekcji): osierocona",
                "Miejsca, których nie udało się wskazać w treści",
                $"[P-GONE] {IssueLabels.Of("P-GONE")}  ·  informacja",
                "Problem: Akapit, którego nie ma.",
                "Miejsce: zniknął",
            ],
            collective.Lines);
    }

    [Fact]
    public void NoIssues_GivesOneComment_NamingTheStandardsThatWereChecked()
    {
        var clean = FixtureCatalog.Build("clean");
        var result = Validate(clean, "firmowy/2.0");
        Assert.Empty(result.Issues);

        var annotated = Writer.Annotate(clean, result, Options);
        var comment = Assert.Single(Added(annotated.Content));

        Assert.Equal(CommentKind.NoIssues, Assert.Single(annotated.Comments).Kind);
        Assert.Equal(
            ["Brak uwag po walidacji", "Dokument spełnia wszystkie automatycznie sprawdzane wymagania standardu: Standard pism firmowych 2.0."],
            comment.Lines);
        Assert.Equal(0, comment.Paragraph);
    }

    [Fact]
    public void NoIssues_ListsEveryStandard_OrNoneWhenUnknown()
    {
        var clean = FixtureCatalog.Build("clean");

        var two = Assert.Single(Added(Writer.Annotate(clean, [], Options with { StandardNames = ["WCAG 2.1 AA", "Hierarchia treści"] }).Content));
        var none = Assert.Single(Added(Writer.Annotate(clean, [], Options).Content));

        Assert.Equal("Dokument spełnia wszystkie automatycznie sprawdzane wymagania standardów: WCAG 2.1 AA; Hierarchia treści.", two.Lines[1]);
        Assert.Equal("Dokument spełnia wszystkie automatycznie sprawdzane wymagania.", none.Lines[1]);
        Assert.DoesNotContain("2.0", none.Text);
    }

    // ------------------------------------------------------------ pakiet

    [Fact]
    public void DocumentWithoutComments_GetsCommentsPart_Relationship_AndContentType()
    {
        var docx = new DocxBuilder().Heading(1, "Wstęp").Paragraph("Treść akapitu.").Build();
        Assert.DoesNotContain("word/comments.xml", FixtureDocx.Parts(docx).Keys);

        var annotated = Writer.Annotate(docx, [Issue("X-1", IssueSeverity.Error, "Opis.", BodyParagraph(docx, "Treść akapitu"))], Options);

        var parts = FixtureDocx.Parts(annotated.Content);
        Assert.Contains("word/comments.xml", parts.Keys);
        Assert.Contains("PartName=\"/word/comments.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml\"", Encoding.UTF8.GetString(parts["[Content_Types].xml"]));
        Assert.Contains("relationships/comments\" Target=\"comments.xml\"", Encoding.UTF8.GetString(parts["word/_rels/document.xml.rels"]));
        Assert.DoesNotContain(parts.Keys, name => name.EndsWith('/'));

        using var package = WordprocessingDocument.Open(new MemoryStream(annotated.Content), false);
        var comment = Assert.Single(package.MainDocumentPart!.WordprocessingCommentsPart!.Comments.Elements<Comment>());
        Assert.Equal("0", comment.Id!.Value);
        Assert.Empty(SchemaErrors(annotated.Content).Except(SchemaErrors(docx)));
        Assert.Equal(MainXml(docx), MainXmlWithoutComments(annotated.Content));
    }

    [Fact]
    public void ExistingComment_IsKept_AndNewIdsContinueAfterIt()
    {
        var annotated = Writer.Annotate(Edge, [Issue("X-1", IssueSeverity.Error, "Opis.", BodyParagraph(Edge, "Ostatni akapit dokumentu"))], Options);

        var all = Read(annotated.Content);

        Assert.Equal([0, 1], all.Select(c => c.Id));
        Assert.Equal(("Jan Kowalski", "JK", "Proszę doprecyzować ten fragment.", "fragment z istniejącym komentarzem"), (all[0].Author, all[0].Initials, all[0].Text, all[0].AnchorText));
        var before = FixtureDocx.Parts(Edge);
        var after = FixtureDocx.Parts(annotated.Content);
        Assert.Equal(before.Keys, after.Keys);
        Assert.True(before["word/_rels/document.xml.rels"].AsSpan().SequenceEqual(after["word/_rels/document.xml.rels"]));
        Assert.True(before["[Content_Types].xml"].AsSpan().SequenceEqual(after["[Content_Types].xml"]));
    }

    // ------------------------------------------------------------ pliki, których nie da się opatrzyć komentarzami

    [Theory]
    [InlineData(WordprocessingDocumentType.MacroEnabledDocument, "dokumentem z makrami (.docm)")]
    [InlineData(WordprocessingDocumentType.Template, "szablonem programu Word (.dotx)")]
    [InlineData(WordprocessingDocumentType.MacroEnabledTemplate, "szablonem z makrami (.dotm)")]
    public void MacroDocumentsAndTemplates_AreRejectedWithExplanation(WordprocessingDocumentType type, string expected)
    {
        using var stream = new MemoryStream();
        stream.Write(new DocxBuilder().Paragraph("Treść.").Build());
        using (var package = WordprocessingDocument.Open(stream, true))
        {
            package.ChangeDocumentType(type);
        }

        var ex = Assert.Throws<CommentExportException>(() => Writer.Annotate(stream.ToArray(), [], Options));

        Assert.Contains(expected, ex.Message);
        Assert.Contains("zapisz plik w Wordzie jako „Dokument programu Word (*.docx)”", ex.Message);
    }

    [Fact]
    public void EmptyCorruptedAndEncryptedFiles_AreRejectedWithPolishMessage()
    {
        byte[] encrypted = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];
        var truncated = Edge.Take(Edge.Length / 2).ToArray();
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = archive.CreateEntry("word/document.xml").Open();
            entry.Write("<nie-xml"u8);
        }

        Assert.Equal("Plik jest pusty.", Assert.Throws<CommentExportException>(() => Writer.Annotate([], [], Options)).Message);
        Assert.StartsWith("Plik jest zaszyfrowany (chroniony hasłem do otwarcia)", Assert.Throws<CommentExportException>(() => Writer.Annotate(encrypted, [], Options)).Message);
        Assert.StartsWith("Nie udało się odczytać pliku jako DOCX", Assert.Throws<CommentExportException>(() => Writer.Annotate("to nie jest plik Worda"u8.ToArray(), [], Options)).Message);
        Assert.StartsWith("Nie udało się odczytać pliku jako DOCX", Assert.Throws<CommentExportException>(() => Writer.Annotate(truncated, [], Options)).Message);
        Assert.StartsWith("Nie udało się", Assert.Throws<CommentExportException>(() => Writer.Annotate(zip.ToArray(), [], Options)).Message);
    }

    [Fact]
    public void UnreadableValidationResult_IsRejectedWithItsReason()
    {
        var garbage = "to nie jest plik Worda"u8.ToArray();
        var result = Validate(garbage, "wcag/2.1");

        var ex = Assert.Throws<CommentExportException>(() => Writer.Annotate(garbage, result, Options));

        Assert.Equal(result.Summary, ex.Message);
    }

    [Fact]
    public void DocumentWithoutParagraphs_IsRejectedWithExplanation()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new Document(new Body(new SectionProperties()));
        }

        var ex = Assert.Throws<CommentExportException>(() => Writer.Annotate(stream.ToArray(), [], Options));

        Assert.Equal("Dokument nie ma w treści żadnego akapitu, więc nie ma gdzie umieścić komentarzy.", ex.Message);
    }

    [Fact]
    public void Date_IsWrittenInUtc_WithSecondPrecision()
    {
        var local = new DateTimeOffset(2025, 3, 1, 12, 0, 5, 987, TimeSpan.FromHours(2));

        var comment = Assert.Single(Added(Writer.Annotate(Edge, [], Options with { Date = local }).Content));

        Assert.Equal("2025-03-01T10:00:05Z", comment.Date);
    }
}
