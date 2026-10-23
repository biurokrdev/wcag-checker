namespace D2.Wcag;

/// <summary>Jedna zastosowana naprawa: kod zgłoszenia, którego dotyczy, i opis tego, co zmieniono.</summary>
public sealed record AppliedFix(string Code, string Description);

public sealed class WcagFixResult
{
    internal WcagFixResult(byte[] document, IReadOnlyList<AppliedFix> applied, WcagValidationResult before, WcagValidationResult after)
    {
        Document = document;
        Applied = applied;
        Before = before;
        After = after;
    }

    /// <summary>Poprawiony DOCX; gdy nic nie zmieniono albo pliku nie dało się odczytać, ten sam ciąg bajtów co na wejściu.</summary>
    public byte[] Document { get; }

    public IReadOnlyList<AppliedFix> Applied { get; }

    public bool Changed => Applied.Count > 0;

    /// <summary>Wynik walidacji przed naprawą.</summary>
    public WcagValidationResult Before { get; }

    /// <summary>Wynik walidacji po naprawie, z tym samym profilem; dla nieczytelnego pliku taki sam jak <see cref="Before"/>.</summary>
    public WcagValidationResult After { get; }

    /// <summary>Zgłoszenia, które pozostały po naprawie i wymagają ręcznej poprawki.</summary>
    public IEnumerable<WcagIssue> Remaining => After.Issues.Where(i => i.Severity != IssueSeverity.Info);
}
