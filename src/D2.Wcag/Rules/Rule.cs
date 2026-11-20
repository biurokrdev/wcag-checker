using D2.Wcag.Docx;

namespace D2.Wcag.Rules;

/// <summary>Jedno trafienie reguły: komunikat, wystąpienia (puste = cały dokument), szczegóły i argumenty instrukcji naprawy.</summary>
public sealed record RuleHit(string Message, IReadOnlyList<IssueLocation?> Locations)
{
    public RuleHit(string message, params IssueLocation?[] locations)
        : this(message, (IReadOnlyList<IssueLocation?>)locations)
    {
    }

    public IReadOnlyList<IssueDetail> Details { get; init; } = [];

    public IReadOnlyList<(string Name, string Value)> FixArgs { get; init; } = [];
}

/// <summary>
/// Jedyny sposób dodania NOWEJ reguły: deskryptor (kod, status, tytuł, instrukcja naprawy) i funkcja wykrywająca.
/// Każde trafienie staje się zgłoszeniem z wagą wynikającą ze statusu deskryptora.
/// </summary>
public sealed class Rule(RuleDescriptor descriptor, Func<DocxDocument, IEnumerable<RuleHit>> detect) : IDocumentRule, IDescribedRule
{
    public RuleDescriptor Descriptor { get; } = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

    public IReadOnlyList<RuleDescriptor> Descriptors => [Descriptor];

    private readonly Func<DocxDocument, IEnumerable<RuleHit>> _detect = detect ?? throw new ArgumentNullException(nameof(detect));

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        foreach (var hit in _detect(document))
        {
            yield return (Descriptor.Issue(hit.Message, [.. hit.FixArgs]) with { Details = hit.Details }).At(hit.Locations);
        }
    }
}
