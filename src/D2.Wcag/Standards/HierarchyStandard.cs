using D2.Wcag.Rules;
using D2.Wcag.Rules.Hierarchy;

namespace D2.Wcag.Standards;

/// <summary>
/// Standard „Hierarchia treści”: nagłówki, listy i tabele tworzą poprawną strukturę dokumentu. Poza profilem
/// domyślnym – wybierany jawnie kluczem <c>hierarchia/1.0</c>. Reguły (kody <c>HIER-…</c>) dokłada się przez
/// <see cref="Rule"/> z deskryptorem: przeskok poziomu nagłówka, treść albo nagłówek bez nagłówka nadrzędnego,
/// pusty i fałszywy nagłówek (WYMAGANE) oraz skok poziomu listy i numer nagłówka niezgodny z poziomem (REKOMENDOWANE).
/// </summary>
public sealed class HierarchyStandard : DocumentStandard
{
    public const string StandardId = "hierarchia";

    public const string StandardVersion = "1.0";

    public static readonly string StandardKey = Standards.StandardKey.Of(StandardId, StandardVersion);

    public HierarchyStandard()
    {
        Rules = HierarchyRules.All;
    }

    public override string Id => StandardId;

    public override string Version => StandardVersion;

    public override string Name => "Hierarchia treści";

    public override IReadOnlyList<IDocumentRule> Rules { get; }

    public override IReadOnlyList<StandardCheck> Checks => [];
}
