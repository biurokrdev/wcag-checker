using System.Text;

namespace D2.Wcag.Docx;

/// <summary>Jeden akapit komentarza: fragmenty tekstu, każdy zwykły albo pogrubiony.</summary>
internal sealed record CommentLine(IReadOnlyList<(string Text, bool Bold)> Segments)
{
    public string Text => string.Concat(Segments.Select(s => s.Text));

    public static CommentLine Plain(string text) => new([(text, false)]);

    public static CommentLine Strong(string text) => new([(text, true)]);
}

/// <summary>Wystąpienie, które trafia do komentarza zbiorczego, z opcjonalnym dopiskiem o miejscu.</summary>
internal sealed record CollectedIssue(WcagIssue Issue, IssueLocation? Location, string? Note);

// Treść komentarzy:
//   [KOD] Tytuł reguły  ·  WAGA (np. WYMAGANE → błąd; bez statusu specyfikacji sama waga)
//   Problem: komunikat zgłoszenia
//   Jak naprawić: instrukcja (pomijana, gdy zgłoszenie jej nie ma – siedzi wtedy w komunikacie)
internal static class CommentText
{
    public static string SeverityLabel(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Error => "błąd",
        IssueSeverity.Warning => "ostrzeżenie",
        _ => "informacja",
    };

    public static string Weight(WcagIssue issue) => issue.Status is { } status
        ? $"{SpecStatuses.Label(status)} → {SeverityLabel(issue.Severity)}"
        : SeverityLabel(issue.Severity);

    public static string Title(WcagIssue issue) => string.IsNullOrWhiteSpace(issue.Title) ? IssueLabels.Of(issue.Code) : issue.Title;

    public static List<CommentLine> ForIssue(WcagIssue issue, string? note)
    {
        var lines = new List<CommentLine>();
        if (!string.IsNullOrWhiteSpace(note))
        {
            lines.Add(CommentLine.Plain(Clean(note)));
        }

        AppendIssue(lines, issue);
        return lines;
    }

    public static List<CommentLine> Collective(IReadOnlyList<CollectedIssue> entries)
    {
        var lines = new List<CommentLine>
        {
            CommentLine.Strong($"Uwagi bez wskazanego miejsca w treści ({Polish.Count(entries.Count, "uwaga", "uwagi", "uwag")})"),
        };

        var groups = entries
            .Select((entry, index) => (Entry: entry, Index: index, Group: GroupOf(entry)))
            .GroupBy(e => e.Group)
            .OrderBy(g => g.Key);
        foreach (var group in groups)
        {
            lines.Add(CommentLine.Strong(GroupTitle(group.Key)));
            var ordered = group
                .OrderByDescending(e => e.Entry.Issue.Severity)
                .ThenBy(e => e.Entry.Issue.Code, StringComparer.Ordinal)
                .ThenBy(e => e.Index);
            foreach (var (entry, _, _) in ordered)
            {
                AppendIssue(lines, entry.Issue);
                if (!string.IsNullOrWhiteSpace(entry.Note))
                {
                    lines.Add(CommentLine.Plain(Clean(entry.Note)));
                }
            }
        }

        return lines;
    }

    public static List<CommentLine> NoIssues(IReadOnlyList<string>? standardNames)
    {
        var names = (standardNames ?? []).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => Clean(n.Trim())).ToList();
        var scope = names.Count switch
        {
            0 => "Dokument spełnia wszystkie automatycznie sprawdzane wymagania.",
            1 => $"Dokument spełnia wszystkie automatycznie sprawdzane wymagania standardu: {names[0]}.",
            _ => $"Dokument spełnia wszystkie automatycznie sprawdzane wymagania standardów: {string.Join("; ", names)}.",
        };
        return [CommentLine.Strong("Brak uwag po walidacji"), CommentLine.Plain(scope)];
    }

    private static void AppendIssue(List<CommentLine> lines, WcagIssue issue)
    {
        lines.Add(new CommentLine([($"[{Clean(issue.Code)}] {Clean(Title(issue))}", true), ($"  ·  {Weight(issue)}", false)]));
        AppendLabeled(lines, "Problem:", issue.Message);
        if (!string.IsNullOrWhiteSpace(issue.Fix))
        {
            AppendLabeled(lines, "Jak naprawić:", issue.Fix);
        }
    }

    // Tekst wielowierszowy (np. instrukcja krok po kroku): pierwszy wiersz za etykietą, kolejne jako osobne akapity.
    private static void AppendLabeled(List<CommentLine> lines, string label, string text)
    {
        var parts = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines.Add(new CommentLine([(label, true), (" " + Clean(parts.Length == 0 ? "" : parts[0]), false)]));
        foreach (var part in parts.Skip(1))
        {
            lines.Add(CommentLine.Plain(Clean(part)));
        }
    }

    private static int GroupOf(CollectedIssue entry) => IssueScopes.Effective(entry.Issue, entry.Location) switch
    {
        IssueScope.Document => 0,
        IssueScope.Styles => 1,
        IssueScope.HeaderFooter => 2,
        IssueScope.Section => 3,
        _ => 4,
    };

    private static string GroupTitle(int group) => group switch
    {
        0 => "Dokument i metadane",
        1 => "Style",
        2 => "Nagłówki i stopki",
        3 => "Sekcje",
        _ => "Miejsca, których nie udało się wskazać w treści",
    };

    // XML 1.0 nie dopuszcza znaków sterujących (poza tabulatorem i końcem wiersza) ani osieroconych surogatów.
    private static string Clean(string text)
    {
        StringBuilder? cleaned = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var valid = c == '\t' || (c >= 0x20 && c < 0xD800) || (c >= 0xE000 && c <= 0xFFFD);
            if (!valid && char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                cleaned?.Append(c).Append(text[i + 1]);
                i++;
                continue;
            }

            if (valid)
            {
                cleaned?.Append(c);
            }
            else
            {
                cleaned ??= new StringBuilder(text, 0, i, text.Length);
                cleaned.Append(' ');
            }
        }

        return cleaned?.ToString() ?? text;
    }
}
