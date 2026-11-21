using D2.Wcag.Rules;

namespace D2.Wcag.Standards;

/// <summary>
/// Standard „Hierarchia treści”: nagłówki, listy i tabele tworzą poprawną strukturę dokumentu. Poza profilem
/// domyślnym – wybierany jawnie kluczem <c>hierarchia/1.0</c>. Reguły (kody <c>HIER-…</c>) dokłada się przez
/// <see cref="Rule"/> z deskryptorem; na razie zestaw reguł jest pusty.
/// </summary>
public sealed class HierarchyStandard : DocumentStandard
{
    public const string StandardId = "hierarchia";

    public const string StandardVersion = "1.0";

    public static readonly string StandardKey = Standards.StandardKey.Of(StandardId, StandardVersion);

    public HierarchyStandard()
    {
        Rules = [];
    }

    public override string Id => StandardId;

    public override string Version => StandardVersion;

    public override string Name => "Hierarchia treści";

    public override IReadOnlyList<IDocumentRule> Rules { get; }

    public override IReadOnlyList<StandardCheck> Checks => [];
}
