using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using D2.Wcag.Rules.Hierarchy;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Tests.Hierarchy;

// Reguły standardu hierarchia/1.0 (kody HIER-…): każda wskazuje akapit i podaje instrukcję naprawy w Wordzie.
public class HierarchyRulesTests
{
    private const int HeadingNumId = 7;

    private static readonly WcagValidationProfile Hierarchy = new WcagValidationProfile().WithStandards(HierarchyStandard.StandardKey);

    private static IReadOnlyList<WcagIssue> Check(byte[] docx, params string[] standards) =>
        new WcagDocxValidator().Validate(docx, standards.Length == 0 ? Hierarchy : new WcagValidationProfile().WithStandards(standards)).Issues;

    private static byte[] Document(Action<Body> content)
    {
        var docx = new FixtureDocx { Content = (_, _, body) => content(body) };
        var numbering = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.Multilevel }) { AbstractNumberId = 7 };
        string[] texts = ["%1.", "%1.%2.", "%1.%2.%3."];
        for (var level = 0; level < texts.Length; level++)
        {
            numbering.Append(Level(level, NumberFormatValues.Decimal, texts[level], null));
        }

        docx.ExtraAbstractNums.Add(numbering);
        docx.ExtraNums.Add(new NumberingInstance(new AbstractNumId { Val = 7 }) { NumberID = HeadingNumId });
        return docx.Build();
    }

    private static Paragraph NumberedHeading(int level, string text, int listLevel) => new(
        new ParagraphProperties(
            new ParagraphStyleId { Val = $"Nagwek{level}" },
            new NumberingProperties(new NumberingLevelReference { Val = listLevel }, new NumberingId { Val = HeadingNumId })),
        Text(text));

    private static List<(string Key, string Snippet)> Places(IEnumerable<WcagIssue> issues, string code) =>
        [.. issues.Where(i => i.Code == code).SelectMany(i => i.Locations).Select(l => (l.Key, l.Snippet))];

    // ------------------------------------------------------------ fixtury

    [Theory]
    [InlineData(HierarchyCodes.Skip, "body:Paragraph:4", "Szczegóły zakresu")]
    [InlineData(HierarchyCodes.NoParent, "body:Paragraph:1", "Ten akapit stoi przed pierwszym nagłówki…")]
    [InlineData(HierarchyCodes.NoParent, "body:Paragraph:2", "Zakres dokumentu")]
    [InlineData(HierarchyCodes.Empty, "body:Paragraph:6", "")]
    [InlineData(HierarchyCodes.Fake, "body:Paragraph:8", "Postanowienia ogólne")]
    [InlineData(HierarchyCodes.ListSkip, "body:Paragraph:17", "pozycja od razu na poziomie trzecim.")]
    [InlineData(HierarchyCodes.NumberingMismatch, "body:Paragraph:43", "Numer z niewłaściwego poziomu")]
    public void HierarchyBroken_DetectsEveryCase_AtItsParagraph(string code, string key, string snippet)
    {
        var docx = FixtureCatalog.Build("hierarchy-broken");

        var issues = Check(docx);

        var issue = Assert.Single(issues, i => i.Code == code && i.Locations.Any(l => l.Key == key));
        var location = Assert.Single(issue.Locations);
        Assert.Equal((LocationKind.Paragraph, snippet), (location.Kind, location.Snippet));
        Assert.Equal(HierarchyStandard.StandardKey, issue.Standard);
        Assert.False(string.IsNullOrWhiteSpace(issue.Fix));
        Assert.Contains("W Wordzie", issue.Fix);
        Assert.DoesNotContain("{", issue.Fix);
        Assert.Equal(IssueScope.Paragraph, issue.Scope);

        // Lokalizacja prowadzi do tego akapitu w dokumencie.
        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(docx), false);
        Assert.IsType<Paragraph>(new DocxDocument(package).Resolve(key));
    }

    [Fact]
    public void HierarchyBroken_ReportsExactlyTheExpectedFindings()
    {
        var issues = Check(FixtureCatalog.Build("hierarchy-broken"));

        Assert.Equal(
            [
                (HierarchyCodes.Empty, 1), (HierarchyCodes.Fake, 1), (HierarchyCodes.ListSkip, 1), (HierarchyCodes.NoParent, 2),
                (HierarchyCodes.NumberingMismatch, 1), (HierarchyCodes.Skip, 1),
            ],
            issues.GroupBy(i => i.Code).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count())));
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("szablon_pisma_2.0")]
    public void CleanDocuments_HaveNoHierarchyFindings(string name)
    {
        var issues = Check(SnapshotCases.Load(name));

        Assert.True(issues.Count == 0, string.Join("; ", issues.Select(i => $"{i.Code}: {i.Message}")));
    }

    [Fact]
    public void Statuses_RequiredGiveErrors_RecommendedGiveWarnings()
    {
        var descriptors = new HierarchyStandard().Descriptors.ToDictionary(d => d.Code, d => (d.Status, d.Severity));

        Assert.Equal(6, descriptors.Count);
        foreach (var code in new[] { HierarchyCodes.Skip, HierarchyCodes.NoParent, HierarchyCodes.Empty, HierarchyCodes.Fake })
        {
            Assert.Equal((SpecStatus.Required, IssueSeverity.Error), descriptors[code]);
        }

        foreach (var code in new[] { HierarchyCodes.ListSkip, HierarchyCodes.NumberingMismatch })
        {
            Assert.Equal((SpecStatus.Recommended, IssueSeverity.Warning), descriptors[code]);
        }

        var issues = Check(FixtureCatalog.Build("hierarchy-broken"));
        Assert.All(issues, i => Assert.Equal((descriptors[i.Code].Status, descriptors[i.Code].Severity), (i.Status!.Value, i.Severity)));
        Assert.All(issues, i => Assert.False(string.IsNullOrWhiteSpace(i.Title)));
    }

    [Fact]
    public void WithWcag_ExistingHeadingFindingsStayNextToHierarchyOnes()
    {
        var issues = Check(FixtureCatalog.Build("hierarchy-broken"), "wcag/2.1", "hierarchia/1.0");

        Assert.Contains(issues, i => i.Code == IssueCodes.HeadingLevelSkipped && i.Standard == "wcag/2.1");
        Assert.Contains(issues, i => i.Code == IssueCodes.FakeHeading && i.Standard == "wcag/2.1");
        Assert.Contains(issues, i => i.Code == IssueCodes.EmptyHeading && i.Standard == "wcag/2.1");
        Assert.Contains(issues, i => i.Code == HierarchyCodes.Skip);
        Assert.Contains(issues, i => i.Code == HierarchyCodes.Fake);
        Assert.Contains(issues, i => i.Code == HierarchyCodes.Empty);
    }

    // ------------------------------------------------------------ HIER-SKIP

    [Fact]
    public void Skip_ReportsJumpDown_ButAllowsReturnToAnyHigherLevel()
    {
        var issues = Check(Document(body => body.Append(
            Heading(1, "Tytuł"), Para("Treść."),
            Heading(2, "Sekcja"), Para("Treść."),
            Heading(3, "Podsekcja"), Para("Treść."),
            Heading(4, "Szczegół"), Para("Treść."),
            Heading(2, "Powrót o dwa poziomy"), Para("Treść."),
            Heading(4, "Przeskok"), Para("Treść."),
            Heading(1, "Drugi tytuł"), Para("Treść."))));

        var issue = Assert.Single(issues);
        Assert.Equal(HierarchyCodes.Skip, issue.Code);
        Assert.Equal("Przeskok", Assert.Single(issue.Locations).Snippet);
        Assert.Contains("poziom 4 zaraz po nagłówku poziomu 2; pominięto poziom 3", issue.Message);
        Assert.Contains("„Nagłówek 3”", issue.Fix);
    }

    // ------------------------------------------------------------ HIER-NO-PARENT

    [Fact]
    public void NoParent_IgnoresTitleStyleAndEmptyParagraphsBeforeTheFirstHeading()
    {
        var docx = new FixtureDocx
        {
            Content = (_, _, body) => body.Append(Styled("Tytu", Text("Tytuł pisma")), new Paragraph(), Heading(1, "Wstęp"), Para("Treść.")),
        };
        docx.ExtraStyles.Add(ParagraphStyle("Tytu", "Title", null, gallery: true));

        Assert.Empty(Check(docx.Build()));
    }

    [Fact]
    public void NoParent_ReportsEveryHeadingWithoutAnAncestor_UntilTheFirstTopLevelHeading()
    {
        var issues = Check(Document(body => body.Append(
            Heading(3, "Najpierw trzeci"), Para("Treść."),
            Heading(2, "Potem drugi"), Para("Treść."),
            Heading(3, "Pod drugim"), Para("Treść."),
            Heading(1, "Wreszcie pierwszy"), Para("Treść."),
            Heading(2, "Pod pierwszym"), Para("Treść."))));

        Assert.Equal(["Najpierw trzeci", "Potem drugi"], Places(issues, HierarchyCodes.NoParent).Select(p => p.Snippet));
        Assert.DoesNotContain(issues, i => i.Code == HierarchyCodes.Skip);
    }

    [Fact]
    public void NoParent_ReportsDocumentWithContentButWithoutAnyHeading_Once()
    {
        var issues = Check(Document(body => body.Append(Para("Pierwszy akapit pisma."), Para("Drugi akapit pisma."))));

        var issue = Assert.Single(issues);
        Assert.Equal(HierarchyCodes.NoParent, issue.Code);
        Assert.Equal("body:Paragraph:0", Assert.Single(issue.Locations).Key);
        Assert.Contains("nie ma żadnego nagłówka", issue.Message);
    }

    [Fact]
    public void EmptyDocument_HasNoHierarchyFindings()
    {
        Assert.Empty(Check(Document(_ => { })));
        Assert.Empty(Check(Document(body => body.Append(new Paragraph()))));
    }

    // ------------------------------------------------------------ HIER-EMPTY

    [Fact]
    public void Empty_ReportsHeadingWithWhitespaceOnly()
    {
        var issues = Check(Document(body => body.Append(
            Heading(1, "Tytuł"), Para("Treść."), Heading(2, "   "), Para("Dalsza treść."))));

        var issue = Assert.Single(issues);
        Assert.Equal((HierarchyCodes.Empty, "body:Paragraph:2"), (issue.Code, Assert.Single(issue.Locations).Key));
        Assert.Contains("Nagłówek poziomu 2 w akapicie 3 jest pusty", issue.Message);
    }

    // ------------------------------------------------------------ HIER-FAKE

    [Fact]
    public void Fake_ReportsOneFindingPerCandidate_AtTheCandidateLocation()
    {
        var docx = Document(body => body.Append(
            Heading(1, "Tytuł"), Para("Treść."),
            Para(Text("Pierwszy fałszywy", new Bold())), Para("Treść."),
            Para(Text("Drugi fałszywy", new FontSize { Val = "32" })), Para("Treść.")));

        var issues = Check(docx).Where(i => i.Code == HierarchyCodes.Fake).ToList();

        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(docx), false);
        var candidates = new DocxDocument(package).FakeHeadingCandidates();
        Assert.Equal(2, candidates.Count);
        Assert.Equal(candidates.Select(c => c.Location.Key), issues.Select(i => Assert.Single(i.Locations).Key));
        Assert.All(issues, i => Assert.Equal("fake-heading", i.DedupGroup));
        Assert.All(issues, i => Assert.Equal(IssueSeverity.Error, i.Severity));
    }

    [Fact]
    public void Fake_SharesItsDedupGroup_SoAnotherStandardWithTheSameGroupIsNotReportedTwice()
    {
        var docx = Document(body => body.Append(Heading(1, "Tytuł"), Para(Text("Fałszywy nagłówek", new Bold())), Para("Treść.")));
        var other = new D2.Wcag.Rules.RuleDescriptor("TEST-FAKE", SpecStatus.Recommended, IssueCategory.Headings, "Fałszywy nagłówek (test)", "Użyj stylu.")
        {
            DedupGroup = HierarchyCodes.FakeHeadingGroup,
        };
        var twin = new TwinStandard(new D2.Wcag.Rules.Rule(other, d => d.FakeHeadingCandidates().Select(c => new D2.Wcag.Rules.RuleHit("Kopia.", c.Location))));
        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(docx), false);

        var result = WcagDocxValidator.Validate(new DocxDocument(package), [twin, new HierarchyStandard()]);

        var fake = Assert.Single(result.Issues, i => i.DedupGroup == HierarchyCodes.FakeHeadingGroup);
        Assert.Equal("TEST-FAKE", fake.Code);
    }

    // ------------------------------------------------------------ HIER-LIST-SKIP

    [Fact]
    public void ListSkip_ReportsJumpAndListStartingBelowTheFirstLevel_ButNotReturns()
    {
        var issues = Check(Document(body => body.Append(
            Heading(1, "Tytuł"),
            Para("Pierwsza lista:"),
            ListItem(HeadingNumId, "poziom pierwszy"),
            ListItem(HeadingNumId, "poziom drugi", 1),
            ListItem(HeadingNumId, "poziom trzeci", 2),
            ListItem(HeadingNumId, "powrót na pierwszy"),
            ListItem(HeadingNumId, "skok na trzeci", 2),
            Para("Druga lista:"),
            ListItem(HeadingNumId, "start od drugiego", 1),
            ListItem(HeadingNumId, "dalej trzeci", 2),
            Para("Koniec."))));

        Assert.All(issues, i => Assert.Equal((HierarchyCodes.ListSkip, IssueSeverity.Warning, IssueCategory.Lists), (i.Code, i.Severity, i.Category)));
        Assert.Equal(["skok na trzeci", "start od drugiego"], Places(issues, HierarchyCodes.ListSkip).Select(p => p.Snippet));
        Assert.Contains("pominięto poziom 2", issues[0].Message);
        Assert.Contains("poziom listy na 2", issues[0].Fix);
        Assert.Contains("brakuje pozycji poziomu 1", issues[1].Message);
        Assert.Contains("poziom listy na 1", issues[1].Fix);
    }

    [Fact]
    public void ListSkip_TreatsListsInSeparateTableCellsSeparately()
    {
        var issues = Check(Document(body =>
        {
            body.Append(Heading(1, "Tytuł"), Para("Tabela 1. Listy."));
            var table = StandardTable("Listy", "Dwie komórki z listami.", [5000, 5000], ["A", "B"], ["x", "y"]);
            var cells = table.Elements<TableRow>().Last().Elements<TableCell>().ToList();
            cells[0].Append(ListItem(BulletNumId, "pozycja w pierwszej komórce"));
            cells[1].Append(ListItem(HeadingNumId, "pozycja w drugiej komórce od razu na trzecim poziomie", 2));
            body.Append(table, Para("Koniec."));
        }));

        var issue = Assert.Single(issues);
        Assert.Equal(HierarchyCodes.ListSkip, issue.Code);
        Assert.Contains("brakuje pozycji poziomu 1", issue.Message);
    }

    // ------------------------------------------------------------ HIER-NUM-MISMATCH

    [Fact]
    public void NumberingMismatch_OnlyWhenHeadingsUseMultilevelNumbering()
    {
        // Bez numeracji nagłówków i z numeracją jednego poziomu – reguła milczy.
        Assert.Empty(Check(Document(body => body.Append(Heading(1, "Tytuł"), Para("Treść."), Heading(2, "Sekcja"), Para("Treść.")))));
        Assert.Empty(Check(Document(body => body.Append(
            Heading(1, "Tytuł"), Para("Treść."),
            NumberedHeading(2, "Pierwsza sekcja", 0), Para("Treść."),
            NumberedHeading(2, "Druga sekcja", 0), Para("Treść.")))));
    }

    [Fact]
    public void NumberingMismatch_AcceptsConstantOffsetBetweenHeadingAndListLevel()
    {
        // H1 bez numeru, H2 z poziomu 1 listy, H3 z poziomu 2 – przesunięcie stałe, więc spójne.
        Assert.Empty(Check(Document(body => body.Append(
            Heading(1, "Tytuł"), Para("Treść."),
            NumberedHeading(2, "Sekcja", 0), Para("Treść."),
            NumberedHeading(3, "Podsekcja", 1), Para("Treść."),
            NumberedHeading(2, "Kolejna sekcja", 0), Para("Treść.")))));
    }

    [Fact]
    public void NumberingMismatch_ReportsHeadingNumberedFromAnotherListLevel()
    {
        var issues = Check(Document(body => body.Append(
            NumberedHeading(1, "Tytuł", 0), Para("Treść."),
            NumberedHeading(2, "Sekcja", 1), Para("Treść."),
            NumberedHeading(2, "Sekcja z numerem tytułu", 0), Para("Treść."),
            NumberedHeading(3, "Podsekcja", 2), Para("Treść."))));

        var issue = Assert.Single(issues);
        Assert.Equal((HierarchyCodes.NumberingMismatch, IssueSeverity.Warning), (issue.Code, issue.Severity));
        Assert.Equal("Sekcja z numerem tytułu", Assert.Single(issue.Locations).Snippet);
        Assert.Contains("ma poziom 2, ale jego numer pochodzi z poziomu 1 listy wielopoziomowej", issue.Message);
        Assert.Contains("poziom 2.", issue.Fix);
    }

    private sealed class TwinStandard(params D2.Wcag.Rules.IDocumentRule[] rules) : DocumentStandard
    {
        public override string Id => "blizniak";

        public override string Version => "1.0";

        public override string Name => "Standard testowy z grupą fake-heading";

        public override IReadOnlyList<D2.Wcag.Rules.IDocumentRule> Rules => rules;
    }
}
