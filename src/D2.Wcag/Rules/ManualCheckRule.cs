using D2.Wcag.Docx;

namespace D2.Wcag.Rules;

// Automat nie oceni sensu tekstów alternatywnych, kontrastu kolorów ani zrozumiałości treści.
internal sealed class ManualCheckRule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        yield return new WcagIssue(
            IssueCodes.ManualCheckSuggested, IssueSeverity.Info, IssueCategory.Document,
            "Zalecane jest ręczne sprawdzenie dostępności w Wordzie (Recenzja → Sprawdź ułatwienia dostępu): treść tekstów alternatywnych, kontrast kolorów i zrozumiałość języka nie są oceniane automatycznie.").At(IssueLocation.Document());
    }
}
