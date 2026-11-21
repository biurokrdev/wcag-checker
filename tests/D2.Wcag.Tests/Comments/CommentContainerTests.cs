using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Tests.CommentExport.CommentTestKit;

namespace D2.Wcag.Tests.CommentExport;

// Kontenery, w których Word nie przyjmuje znaczników komentarza. Kontrolka treści zwykłego tekstu, daty, listy,
// pola wyboru, obrazu (i każda inna niż tekst sformatowany) ze znacznikiem w w:sdtContent daje plik, którego Word
// nie otwiera („Plik jest prawdopodobnie uszkodzony”) – potwierdzone otwarciem w Wordzie dla każdego typu poniżej.
// Znaczniki muszą stać poza taką kontrolką; kolejność komentarzy w jednym miejscu to waga, potem kod.
// Kilka wejść jest celowo sztucznych i sam Word ich nie otwiera (kontrolka obrazu z tekstem zamiast rysunku, wierszowa
// sekcja powtarzana, kontrolka albo łącze zagnieżdżone w kontrolce zwykłego tekstu) – tu liczy się tylko reguła
// „żadnego znacznika w zamkniętej kontrolce”; prawdziwa kontrolka obrazu była sprawdzona w Wordzie osobno.
public class CommentContainerTests
{
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string Ns = $"xmlns:w=\"{W}\" xmlns:w14=\"http://schemas.microsoft.com/office/word/2010/wordml\" xmlns:w15=\"http://schemas.microsoft.com/office/word/2012/wordml\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";

    private static readonly WcagCommentWriter Writer = new();

    /// <summary>Właściwości kontrolek „zamkniętych” (nazwa przypadku → dzieci w:sdtPr).</summary>
    public static readonly IReadOnlyDictionary<string, string> Closed = new Dictionary<string, string>
    {
        ["text"] = "<w:text/>",
        ["textMultiLine"] = "<w:text w:multiLine=\"1\"/>",
        ["date"] = "<w:date><w:dateFormat w:val=\"yyyy-MM-dd\"/><w:lid w:val=\"pl-PL\"/><w:storeMappedDataAs w:val=\"dateTime\"/><w:calendar w:val=\"gregorian\"/></w:date>",
        ["dropDownList"] = "<w:dropDownList><w:listItem w:displayText=\"CEL\" w:value=\"CEL\"/><w:listItem w:displayText=\"B\" w:value=\"B\"/></w:dropDownList>",
        ["comboBox"] = "<w:comboBox><w:listItem w:displayText=\"CEL\" w:value=\"CEL\"/></w:comboBox>",
        ["checkbox"] = "<w14:checkbox><w14:checked w14:val=\"0\"/><w14:checkedState w14:val=\"2612\" w14:font=\"MS Gothic\"/><w14:uncheckedState w14:val=\"2610\" w14:font=\"MS Gothic\"/></w14:checkbox>",
        ["picture"] = "<w:picture/>",
        ["lockedText"] = "<w:lock w:val=\"sdtContentLocked\"/><w:text/>",
        ["placeholder"] = "<w:showingPlcHdr/>",
        ["dataBinding"] = "<w:dataBinding w:xpath=\"/root/a\" w:storeItemID=\"{00000000-0000-0000-0000-000000000001}\"/>",
        ["repeatingSection"] = "<w15:repeatingSection/>",
        ["unknownExtension"] = "<w15:webExtensionLinked/>",
    };

    /// <summary>Właściwości kontrolek, w których Word pozwala komentować wnętrze.</summary>
    public static readonly IReadOnlyDictionary<string, string> Open = new Dictionary<string, string>
    {
        ["none"] = "",
        ["richText"] = "<w:richText/>",
        ["group"] = "<w:group/>",
        ["citation"] = "<w:citation/>",
        ["equation"] = "<w:equation/>",
        ["bibliography"] = "<w:bibliography/>",
        ["docPartObj"] = "<w:docPartObj><w:docPartGallery w:val=\"Quick Parts\"/></w:docPartObj>",
        ["lockedRich"] = "<w:alias w:val=\"Pole\"/><w:tag w:val=\"pole\"/><w:lock w:val=\"sdtContentLocked\"/><w:placeholder><w:docPart w:val=\"DefaultPlaceholder_1\"/></w:placeholder>",
    };

    public static IEnumerable<object[]> ClosedKinds() => Closed.Keys.Select(k => new object[] { k });

    public static IEnumerable<object[]> OpenKinds() => Open.Keys.Select(k => new object[] { k });

    private static string R(string text) => $"<w:r><w:t xml:space=\"preserve\">{text}</w:t></w:r>";

    private static string Sdt(string properties, string content, int id = 501) =>
        $"<w:sdt><w:sdtPr><w:id w:val=\"{id}\"/>{properties}</w:sdtPr><w:sdtContent>{content}</w:sdtContent></w:sdt>";

    /// <summary>Akapit „Przed {inner} po.” z elementem wierszowym w środku.</summary>
    private static string P(string inner) => $"<w:p {Ns}>{R("Przed ")}{inner}{R(" po.")}</w:p>";

    /// <summary>Dokument: [„Tytuł”] + podane bloki + [„Koniec.”].</summary>
    private static byte[] Document(string blocks, bool title = true, bool end = true)
    {
        var xml = $"<w:body {Ns}>{(title ? $"<w:p>{R("Tytuł")}</w:p>" : "")}{blocks}{(end ? $"<w:p>{R("Koniec.")}</w:p>" : "")}</w:body>";
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new Document(new Body(xml));
        }

        return stream.ToArray();
    }

    private static WcagIssue Issue(string code, IssueSeverity severity, IssueLocation location) =>
        new WcagIssue(code, severity, IssueCategory.Text, "Opis " + code).At(location);

    private static IssueLocation Target(byte[] docx) => Locate(docx, d => d.Body.Descendants<Run>().First(r => r.InnerText == "CEL"));

    private static IssueLocation TargetParagraph(byte[] docx) =>
        Locate(docx, d => d.Body.Descendants<Run>().First(r => r.InnerText == "CEL").Ancestors<Paragraph>().First());

    /// <summary>Znaczniki komentarzy audytora, które wylądowały wewnątrz jakiejkolwiek kontrolki treści o podanym id.</summary>
    private static List<OpenXmlElement> MarkersInsideControl(byte[] docx, int id = 501)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        return package.MainDocumentPart!.Document.Body!.Descendants<SdtElement>()
            .Where(s => s.GetFirstChild<SdtProperties>()?.GetFirstChild<SdtId>()?.Val?.Value == id)
            .SelectMany(s => s.Descendants())
            .Where(e => e is CommentRangeStart or CommentRangeEnd or CommentReference)
            .Select(e => (OpenXmlElement)e.CloneNode(true))
            .ToList();
    }

    // COMMENT_TEST_DUMP=<katalog>: zapisuje wyniki testów tej klasy, żeby dało się je otworzyć w Wordzie (weryfikacja ręczna).
    private static int _dumped;

    private static void AssertIntact(byte[] original, byte[] annotated, [System.Runtime.CompilerServices.CallerMemberName] string test = "")
    {
        if (Environment.GetEnvironmentVariable("COMMENT_TEST_DUMP") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            var name = $"{test}_{Interlocked.Increment(ref _dumped):000}";
            File.WriteAllBytes(Path.Combine(folder, name + ".docx"), annotated);
            File.WriteAllBytes(Path.Combine(folder, name + "_wejscie.docx"), original);
        }

        Assert.Equal(MainXml(original), MainXmlWithoutComments(annotated));
        Assert.Empty(SchemaErrors(annotated).Except(SchemaErrors(original)));
    }

    // ------------------------------------------------------------ kontrolki wierszowe

    [Theory]
    [MemberData(nameof(ClosedKinds))]
    public void RunInClosedInlineControl_IsAnchoredAroundTheWholeControl(string kind)
    {
        var docx = Document(P(Sdt(Closed[kind], R("CEL"))));

        var annotated = Writer.Annotate(docx, [Issue("T-RUN", IssueSeverity.Error, Target(docx))], Options);

        Assert.Empty(MarkersInsideControl(annotated.Content));
        var comment = Assert.Single(Added(annotated.Content));
        Assert.Equal("CEL", comment.AnchorText);
        Assert.Equal(1, comment.Paragraph);

        // Znaczniki stoją bezpośrednio przy kontrolce, jako jej rodzeństwo w akapicie – tak zapisuje je Word.
        using var package = WordprocessingDocument.Open(new MemoryStream(annotated.Content), false);
        var control = package.MainDocumentPart!.Document.Body!.Descendants<SdtRun>().Single();
        Assert.IsType<Paragraph>(control.Parent);
        Assert.IsType<CommentRangeStart>(control.PreviousSibling());
        Assert.IsType<CommentRangeEnd>(control.NextSibling());
        Assert.NotNull(control.NextSibling()!.NextSibling<Run>()!.GetFirstChild<CommentReference>());
        AssertIntact(docx, annotated.Content);
    }

    [Theory]
    [MemberData(nameof(OpenKinds))]
    public void RunInRichTextInlineControl_IsAnchoredInside(string kind)
    {
        var docx = Document(P(Sdt(Open[kind], R("CEL") + R(" reszta"))));

        var annotated = Writer.Annotate(docx, [Issue("T-RUN", IssueSeverity.Error, Target(docx))], Options);

        Assert.Equal("CEL", Assert.Single(Added(annotated.Content)).AnchorText);
        Assert.Equal(3, MarkersInsideControl(annotated.Content).Count);
        AssertIntact(docx, annotated.Content);
    }

    [Fact]
    public void ClosedInlineControl_InsideHyperlink_AndRichControlInsideClosedOne_StayUntouched()
    {
        var inLink = Document(P($"<w:hyperlink w:anchor=\"x\">{Sdt(Closed["text"], R("CEL"))}</w:hyperlink>"));
        var nested = Document(P(Sdt(Closed["text"], Sdt(Open["richText"], R("CEL"), id: 502))));
        var linkInClosed = Document(P(Sdt(Closed["dropDownList"], $"<w:hyperlink w:anchor=\"x\">{R("CEL")}</w:hyperlink>")));

        foreach (var docx in new[] { inLink, nested, linkInClosed })
        {
            var annotated = Writer.Annotate(docx, [Issue("T-RUN", IssueSeverity.Error, Target(docx))], Options);

            Assert.Empty(MarkersInsideControl(annotated.Content));
            Assert.Empty(MarkersInsideControl(annotated.Content, 502));
            Assert.Equal("CEL", Assert.Single(Added(annotated.Content)).AnchorText);
            AssertIntact(docx, annotated.Content);
        }
    }

    [Fact]
    public void RangeEndingInClosedControl_CoversTheWholeControl()
    {
        var docx = Document(P(Sdt(Closed["text"], R("CEL") + R(" i dalej"))));
        var paragraph = TargetParagraph(docx) with { Range = new TextRange("Przed ".Length - 3, 6) };

        var annotated = Writer.Annotate(docx, [Issue("T-RANGE", IssueSeverity.Error, paragraph)], Options);

        Assert.Empty(MarkersInsideControl(annotated.Content));
        Assert.Equal("Przed CEL i dalej", Assert.Single(Added(annotated.Content)).AnchorText);
        AssertIntact(docx, annotated.Content);
    }

    [Fact]
    public void WholeParagraphWithClosedInlineControl_KeepsMarkersAsParagraphChildren()
    {
        var docx = Document($"<w:p {Ns}>{Sdt(Closed["date"], R("CEL"))}</w:p>");

        var annotated = Writer.Annotate(docx, [Issue("T-PAR", IssueSeverity.Error, TargetParagraph(docx))], Options);

        Assert.Empty(MarkersInsideControl(annotated.Content));
        Assert.Equal("CEL", Assert.Single(Added(annotated.Content)).AnchorText);
        AssertIntact(docx, annotated.Content);
    }

    // ------------------------------------------------------------ kontrolki na poziomie bloku, komórki, wiersza

    [Theory]
    [MemberData(nameof(ClosedKinds))]
    public void ContentOfClosedBlockControl_IsCommentedOnTheParagraphBeforeIt(string kind)
    {
        var docx = Document(Sdt(Closed[kind], $"<w:p>{R("CEL")}</w:p>"));

        var annotated = Writer.Annotate(
            docx, [Issue("T-RUN", IssueSeverity.Error, Target(docx)), Issue("T-PAR", IssueSeverity.Warning, TargetParagraph(docx))], Options);

        Assert.Empty(MarkersInsideControl(annotated.Content));
        var comments = Added(annotated.Content);
        Assert.Equal(2, comments.Count);
        Assert.All(comments, c =>
        {
            Assert.Equal("Tytuł", c.AnchorText);
            Assert.Equal("Dotyczy kontrolki treści za tym akapitem (Word nie pozwala komentować wnętrza kontrolki tego typu): CEL", c.Lines[0]);
        });
        Assert.Equal(["[T-RUN]", "[T-PAR]"], comments.Select(c => c.Lines[1].Split(' ')[0]));
        AssertIntact(docx, annotated.Content);
    }

    [Theory]
    [MemberData(nameof(OpenKinds))]
    public void ContentOfRichTextBlockControl_IsCommentedInPlace(string kind)
    {
        var docx = Document(Sdt(Open[kind], $"<w:p>{R("CEL")}</w:p>"));

        var annotated = Writer.Annotate(docx, [Issue("T-PAR", IssueSeverity.Warning, TargetParagraph(docx))], Options);

        var comment = Assert.Single(Added(annotated.Content));
        Assert.Equal("CEL", comment.AnchorText);
        Assert.Equal(2, comment.Lines.Count);
        Assert.Equal(3, MarkersInsideControl(annotated.Content).Count);
        AssertIntact(docx, annotated.Content);
    }

    [Fact]
    public void ClosedBlockControl_AtDocumentStart_MovesCommentsToTheParagraphAfterIt_IncludingTheCollectiveOne()
    {
        var docx = Document(Sdt(Closed["text"], $"<w:p>{R("CEL")}</w:p>"), title: false);

        var annotated = Writer.Annotate(
            docx,
            [Issue("T-RUN", IssueSeverity.Error, Target(docx)), new WcagIssue(IssueCodes.TitleMissing, IssueSeverity.Error, IssueCategory.Metadata, "Brak tytułu.")],
            Options);

        Assert.Empty(MarkersInsideControl(annotated.Content));
        var comments = Added(annotated.Content);
        Assert.Equal([CommentKind.Collective, CommentKind.Occurrence], annotated.Comments.Select(c => c.Kind));
        Assert.All(comments, c => Assert.Equal("Koniec.", c.AnchorText));
        Assert.Equal("Dotyczy kontrolki treści przed tym akapitem (Word nie pozwala komentować wnętrza kontrolki tego typu): CEL", comments[1].Lines[0]);
        AssertIntact(docx, annotated.Content);
    }

    [Fact]
    public void ClosedControlAroundTableCell_AndRow_MovesCommentsOutside()
    {
        string Table(string rows) => $"<w:tbl><w:tblPr><w:tblW w:w=\"5000\" w:type=\"pct\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"5000\"/></w:tblGrid>{rows}</w:tbl>";
        var cellContent = Document(Table($"<w:tr><w:tc>{Sdt(Closed["text"], $"<w:p>{R("CEL")}</w:p>")}<w:p/></w:tc></w:tr>"));
        var aroundCell = Document(Table($"<w:tr>{Sdt(Closed["text"], $"<w:tc><w:p>{R("CEL")}</w:p></w:tc>")}</w:tr>"));
        var aroundRow = Document(Table(Sdt(Closed["repeatingSection"], $"<w:tr><w:tc><w:p>{R("CEL")}</w:p></w:tc></w:tr>")));

        foreach (var docx in new[] { cellContent, aroundCell, aroundRow })
        {
            var cell = Locate(docx, d => d.Body.Descendants<TableCell>().First());
            var table = Locate(docx, d => d.Body.Descendants<Table>().First());

            var annotated = Writer.Annotate(
                docx,
                [Issue("T-RUN", IssueSeverity.Error, Target(docx)), Issue("T-CELL", IssueSeverity.Warning, cell), Issue("T-TABLE", IssueSeverity.Info, table)],
                Options);

            Assert.Empty(MarkersInsideControl(annotated.Content));
            Assert.Equal(3, annotated.Comments.Count(c => c.Kind == CommentKind.Occurrence));
            Assert.Equal(3, Added(annotated.Content).Count);
            AssertIntact(docx, annotated.Content);
        }
    }

    [Fact]
    public void DocumentWithOnlyAClosedBlockControl_IsRejected()
    {
        var docx = Document(Sdt(Closed["text"], $"<w:p>{R("CEL")}</w:p>"), title: false, end: false);

        var ex = Assert.Throws<CommentExportException>(() => Writer.Annotate(docx, [Issue("T-RUN", IssueSeverity.Error, Target(docx))], Options));

        Assert.Equal("Dokument nie ma w treści żadnego akapitu, w którym Word pozwala umieścić komentarz.", ex.Message);
    }

    // ------------------------------------------------------------ inne kontenery wierszowe

    [Theory]
    [InlineData("fldSimple", "<w:fldSimple w:instr=\" AUTHOR \">{0}</w:fldSimple>", false)]
    [InlineData("del", "<w:del w:id=\"901\" w:author=\"A\" w:date=\"2024-01-01T00:00:00Z\"><w:r><w:delText>CEL</w:delText></w:r></w:del>", false)]
    [InlineData("bdo", "<w:bdo w:val=\"rtl\">{0}</w:bdo>", false)]
    [InlineData("customXml", "<w:customXml w:uri=\"urn:x\" w:element=\"e\">{0}</w:customXml>", false)]
    [InlineData("ruby", "<w:r><w:ruby><w:rubyPr><w:rubyAlign w:val=\"center\"/><w:hps w:val=\"10\"/><w:hpsRaise w:val=\"18\"/><w:hpsBaseText w:val=\"20\"/><w:lid w:val=\"ja-JP\"/></w:rubyPr><w:rt>{0}</w:rt><w:rubyBase><w:r><w:t>baza</w:t></w:r></w:rubyBase></w:ruby></w:r>", false)]
    [InlineData("hyperlink", "<w:hyperlink w:anchor=\"x\">{0}</w:hyperlink>", true)]
    [InlineData("ins", "<w:ins w:id=\"900\" w:author=\"A\" w:date=\"2024-01-01T00:00:00Z\">{0}</w:ins>", true)]
    public void RunInOtherInlineContainer_IsAnchoredInsideOnlyWhereWordAcceptsIt(string container, string template, bool inside)
    {
        var docx = Document(P(string.Format(template, R("CEL"))));
        var target = Locate(docx, d => d.Body.Descendants<Run>().First(r => r.InnerText == "CEL" && !r.Descendants<Ruby>().Any()));

        var annotated = Writer.Annotate(docx, [Issue("T-RUN", IssueSeverity.Error, target)], Options);

        using var package = WordprocessingDocument.Open(new MemoryStream(annotated.Content), false);
        var start = package.MainDocumentPart!.Document.Body!.Descendants<CommentRangeStart>().Single();
        var reference = package.MainDocumentPart.Document.Body!.Descendants<CommentReference>().Single().Parent!;
        if (inside)
        {
            Assert.Equal(container, start.Parent!.LocalName);
            Assert.Equal(container, reference.Parent!.LocalName);
        }
        else
        {
            Assert.IsType<Paragraph>(start.Parent);
            Assert.IsType<Paragraph>(reference.Parent);
        }

        AssertIntact(docx, annotated.Content);
    }

    // ------------------------------------------------------------ kolejność przy mieszanych kotwicach

    [Fact]
    public void CommentsStartingAtTheSamePlace_AreOrderedBySeverityThenCode_WhateverTheAnchorKind()
    {
        var docx = Document($"<w:p {Ns}><w:bookmarkStart w:id=\"7\" w:name=\"z\"/>{R("CEL")}<w:bookmarkEnd w:id=\"7\"/>{R(" środek")}{R(" koniec")}</w:p>");
        var paragraph = TargetParagraph(docx);
        var first = Target(docx);
        var last = Locate(docx, d => d.Body.Descendants<Run>().First(r => r.InnerText == " koniec"));
        var range = paragraph with { Range = new TextRange(0, "CEL środek".Length) };

        var annotated = Writer.Annotate(
            docx,
            [
                Issue("C-PAR-WARN", IssueSeverity.Warning, paragraph),
                Issue("D-RUN-INFO", IssueSeverity.Info, first),
                Issue("B-RUN-WARN", IssueSeverity.Warning, first),
                Issue("Z-LAST-ERR", IssueSeverity.Error, last),
                Issue("B-PAR-ERR", IssueSeverity.Error, paragraph),
                Issue("A-RANGE-INFO", IssueSeverity.Info, range),
                Issue("A-RUN-ERR", IssueSeverity.Error, first),
            ],
            Options);

        var comments = Added(annotated.Content);
        Assert.Equal(
            ["A-RUN-ERR", "B-PAR-ERR", "B-RUN-WARN", "C-PAR-WARN", "A-RANGE-INFO", "D-RUN-INFO", "Z-LAST-ERR"],
            comments.Select(c => c.Lines[0].Split(']')[0][1..]));
        Assert.Equal(Enumerable.Range(0, 7), comments.Select(c => c.Id));
        Assert.Equal(
            ["CEL", "CEL środek koniec", "CEL", "CEL środek koniec", "CEL środek", "CEL", " koniec"],
            comments.Select(c => c.AnchorText));

        // Początki zakresów stoją w treści w kolejności komentarzy.
        using var package = WordprocessingDocument.Open(new MemoryStream(annotated.Content), false);
        Assert.Equal(
            ["0", "1", "2", "3", "4", "5", "6"],
            package.MainDocumentPart!.Document.Body!.Descendants<CommentRangeStart>().Select(m => m.Id!.Value));
        AssertIntact(docx, annotated.Content);

        // Ten sam wynik przy odwróconej kolejności zgłoszeń na wejściu.
        var reversed = Writer.Annotate(
            docx,
            [
                Issue("A-RUN-ERR", IssueSeverity.Error, first), Issue("A-RANGE-INFO", IssueSeverity.Info, range), Issue("B-PAR-ERR", IssueSeverity.Error, paragraph),
                Issue("Z-LAST-ERR", IssueSeverity.Error, last), Issue("B-RUN-WARN", IssueSeverity.Warning, first), Issue("D-RUN-INFO", IssueSeverity.Info, first),
                Issue("C-PAR-WARN", IssueSeverity.Warning, paragraph),
            ],
            Options);
        Assert.Equal(annotated.Content, reversed.Content);
    }
}
