using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Corporate20;

// Naprawy standardu 2.0 mają te same warunki co reguły liczone na wartościach efektywnych: po naprawie reguła nie
// zgłasza, a naprawa nie zmienia tego, czego reguła nie zgłaszała.
public class Fixes20ConsistencyTests
{
    private static WcagFixResult Fix(byte[] docx) => new WcagDocxFixer().Fix(docx, Audit20.Only20);

    private static List<T> Elements<T>(byte[] docx, Func<MainDocumentPart, IEnumerable<T>> select)
    {
        using var stream = new MemoryStream(docx);
        using var package = WordprocessingDocument.Open(stream, false);
        return select(package.MainDocumentPart!).ToList();
    }

    [Fact]
    public void Gaps_CantSplitFix_TouchesOnlyTheRowTheRuleReports()
    {
        var gaps = FixtureCatalog.Build("audit20-gaps");

        var result = Fix(gaps);

        Assert.StartsWith("Tabela 1: 1 wiersz", Assert.Single(result.Before.Of(IssueCodes.TableRowCanSplit)).Message);
        var applied = Assert.Single(result.Applied, a => a.Code == IssueCodes.TableRowCanSplit);
        Assert.Equal("Zablokowano dzielenie między strony dla 1 wiersza tabel.", applied.Description);
        Assert.Empty(result.After.Of(IssueCodes.TableRowCanSplit));

        // Tabela 2 ma „nie dziel wiersza” ze stylu tabeli: jej wiersze zostają bez w:cantSplit.
        bool[] Direct(byte[] docx, int table) => [.. Elements(docx, main => main.Document.Body!.Elements<Table>().ElementAt(table).Elements<TableRow>()
            .Select(r => r.TableRowProperties?.GetFirstChild<CantSplit>() is not null))];
        Assert.Equal([true, true, false], Direct(gaps, 0));
        Assert.Equal([true, true, true], Direct(result.Document, 0));
        Assert.Equal([false, false, false], Direct(gaps, 1));
        Assert.Equal([false, false, false], Direct(result.Document, 1));
    }

    [Fact]
    public void JustificationFix_LeavesEmptyJustifiedParagraphsAlone()
    {
        Paragraph Justified(string? text)
        {
            var paragraph = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Both }));
            if (text is not null)
            {
                paragraph.Append(new Run(new Text(text)));
            }

            return paragraph;
        }

        var document = DocxBuilder.Compliant20().Heading(1, "A").Add(Justified("Wyjustowany akapit.")).Add(Justified(null)).Build20();

        var result = Fix(document);

        Assert.Single(Assert.Single(result.Before.Of(IssueCodes.JustificationForbidden)).Locations);
        Assert.Equal("Wyrównano do lewej 1 akapit.", Assert.Single(result.Applied).Description);
        Assert.Empty(result.After.Issues);
        var alignments = Elements(result.Document, main => main.Document.Body!.Elements<Paragraph>().Skip(1).Select(p => p.ParagraphProperties!.Justification!.Val!.Value));
        Assert.Equal([JustificationValues.Left, JustificationValues.Both], alignments);
    }

    [Fact]
    public void TableHeaderFix_SkipsLayoutTables_WhichTheRuleDoesNotJudge()
    {
        var data = DocxBuilder.DataTable20(t => t.Elements<TableRow>().First().TableRowProperties!.RemoveAllChildren<TableHeader>());
        var layout = new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("Ramka")))), new TableCell(new Paragraph(new Run(new Text("układu"))))));
        var document = DocxBuilder.Compliant20().Heading(1, "A").Paragraph("Tabela 1.").Add(data).Paragraph("Ramka:").Add(layout).Build20();

        var result = Fix(document);

        Assert.Equal("body:Table:0", Assert.Single(result.Before.Of(IssueCodes.TableMissingHeaders)).Locations.Single().Key);
        Assert.Equal("Oznaczono pierwszy wiersz jako nagłówkowy w 1 tabeli.", Assert.Single(result.Applied).Description);
        Assert.Empty(result.After.Of(IssueCodes.TableMissingHeaders));
        var headers = Elements(result.Document, main => main.Document.Body!.Elements<Table>().Select(t => t.Elements<TableRow>().First().TableRowProperties?.GetFirstChild<TableHeader>() is not null));
        Assert.Equal([true, false], headers);
    }

    // Wszystkie dokumenty testowe: naprawa działa tylko wtedy, gdy reguła zgłosiła któryś z jej kodów, po naprawie
    // kod naprawy nie jest już zgłaszany, a druga naprawa niczego nie zmienia.
    [Fact]
    public void OnAllTestDocuments_FixesFollowTheRules()
    {
        var fixes = new CorporateStandard20().Fixes;
        foreach (var name in SnapshotCases.Documents)
        {
            var result = Fix(SnapshotCases.Load(name));
            var reported = result.Before.Issues.Select(i => i.Code).ToHashSet();

            Assert.All(result.Applied, applied =>
            {
                var fix = Assert.Single(fixes, f => f.Codes.Contains(applied.Code));
                Assert.True(fix.Codes.Any(reported.Contains), $"{name}: naprawa {applied.Code} zadziałała bez zgłoszenia reguły.");
                Assert.DoesNotContain(result.After.Issues, i => i.Code == applied.Code);
            });
            Assert.Empty(Fix(result.Document).Applied);
        }

        Assert.Empty(Fix(FixtureCatalog.Build("clean")).Applied);
    }
}
