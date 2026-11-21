using D2.Wcag.Rules;

namespace D2.Wcag.Standards;

/// <summary>
/// Standard „Formatowanie z Galerii stylów”: treść ma być formatowana stylami, nie formatowaniem bezpośrednim.
/// Poza profilem domyślnym – wybierany jawnie kluczem <c>style/1.0</c>. Reguły (kody <c>STY-…</c>) dokłada się
/// przez <see cref="Rule"/> z deskryptorem; na razie zestaw reguł jest pusty.
/// </summary>
public sealed class StyleGalleryStandard : DocumentStandard
{
    public const string StandardId = "style";

    public const string StandardVersion = "1.0";

    public static readonly string StandardKey = Standards.StandardKey.Of(StandardId, StandardVersion);

    public StyleGalleryStandard(StyleGalleryOptions? options = null)
    {
        Options = options ?? new StyleGalleryOptions();
        Rules = [];
    }

    public StyleGalleryOptions Options { get; }

    public override string Id => StandardId;

    public override string Version => StandardVersion;

    public override string Name => "Formatowanie z Galerii stylów";

    public override IReadOnlyList<IDocumentRule> Rules { get; }

    public override IReadOnlyList<StandardCheck> Checks => [];
}
