namespace D2.Wcag.Rules;

/// <summary>
/// Opis jednej reguły do rejestru standardu: kod, status w specyfikacji (z niego waga), kategoria, tytuł po polsku
/// i instrukcja naprawy. Nowe reguły nie dopisują kodów do <see cref="IssueCodes"/> ani <see cref="IssueLabels"/> –
/// tytuł niesie zgłoszenie.
/// </summary>
/// <param name="Code">Kod zgłoszenia, np. <c>STY-DIRECT-REDUNDANT</c> (z myślnikami, żeby nie kolidował z <see cref="IssueCodes"/>).</param>
/// <param name="Fix">Szablon instrukcji naprawy; miejsca <c>{nazwa}</c> wypełnia <see cref="Issue"/>.</param>
public sealed record RuleDescriptor(string Code, SpecStatus Status, IssueCategory Category, string Title, string Fix,
                                    IssueScope Scope = IssueScope.Paragraph)
{
    /// <summary>Odwołanie do specyfikacji, np. „G-11”, „rozdz. 7”.</summary>
    public string? SpecRef { get; init; }

    /// <summary>Grupa deduplikacji między standardami (patrz <see cref="WcagIssue.DedupGroup"/>).</summary>
    public string? DedupGroup { get; init; }

    public IssueSeverity Severity => SpecStatuses.Severity(Status);

    /// <summary>
    /// Zgłoszenie tej reguły: waga ze statusu, tytuł, zakres i instrukcja naprawy z deskryptora
    /// (<paramref name="fixArgs"/> podmieniają <c>{nazwa}</c> w szablonie <see cref="Fix"/>).
    /// </summary>
    public WcagIssue Issue(string message, params (string Name, string Value)[] fixArgs)
    {
        var fix = Fix;
        foreach (var (name, value) in fixArgs ?? [])
        {
            fix = fix.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return new WcagIssue(Code, Severity, Category, message)
        {
            Status = Status,
            Title = Title,
            Fix = fix,
            Scope = Scope,
            DedupGroup = DedupGroup,
        };
    }
}

/// <summary>Reguła, która opisuje swoje zgłoszenia deskryptorami (rejestr standardu, test unikalności kodów).</summary>
public interface IDescribedRule
{
    IReadOnlyList<RuleDescriptor> Descriptors { get; }
}
