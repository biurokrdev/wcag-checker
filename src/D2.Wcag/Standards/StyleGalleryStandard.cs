using D2.Wcag.Rules;
using D2.Wcag.Rules.StyleGallery;

namespace D2.Wcag.Standards;

/// <summary>
/// Standard „Formatowanie z Galerii stylów”: treść ma być formatowana stylami, nie formatowaniem bezpośrednim.
/// Poza profilem domyślnym – wybierany jawnie kluczem <c>style/1.0</c>. Reguły (kody <c>STY-…</c>, patrz
/// <see cref="StyleCodes"/>): formatowanie ręczne takie samo jak w stylu i inne niż w stylu (jedno zgłoszenie na akapit),
/// akapit udający nagłówek, styl spoza Galerii stylów i kopia stylu. Statusy reguł, lista ignorowanych właściwości
/// i zakres (tabele, nagłówki i stopki stron) są w <see cref="StyleGalleryOptions"/>.
/// </summary>
public sealed class StyleGalleryStandard : DocumentStandard
{
    public const string StandardId = "style";

    public const string StandardVersion = "1.0";

    public static readonly string StandardKey = Standards.StandardKey.Of(StandardId, StandardVersion);

    public StyleGalleryStandard(StyleGalleryOptions? options = null)
    {
        Options = options ?? new StyleGalleryOptions();
        var analyzer = new StyleAnalyzer(Options);
        Rules =
        [
            DirectFormattingRules.Override(analyzer),
            DirectFormattingRules.Redundant(analyzer),
            FakeHeadingRule.Create(analyzer),
            StyleUsageRules.NotInGallery(analyzer),
            StyleUsageRules.Duplicate(analyzer),
        ];
    }

    public StyleGalleryOptions Options { get; }

    public override string Id => StandardId;

    public override string Version => StandardVersion;

    public override string Name => "Formatowanie z Galerii stylów";

    public override IReadOnlyList<IDocumentRule> Rules { get; }

    public override IReadOnlyList<StandardCheck> Checks =>
    [
        new("sty-override", "Formatowanie bezpośrednie", "Brak formatowania ręcznego innego niż w stylu", [StyleCodes.DirectOverride]),
        new("sty-redundant", "Formatowanie bezpośrednie", "Brak formatowania ręcznego powtarzającego styl", [StyleCodes.DirectRedundant]),
        new("sty-fake-heading", "Style", "Nagłówki mają styl nagłówka, a nie tylko wygląd", [StyleCodes.FakeHeading]),
        new("sty-gallery", "Style", "Użyte style są w Galerii stylów", [StyleCodes.NotInGallery]),
        new("sty-duplicate", "Style", "Brak kopii stylów po wklejeniu z innego dokumentu", [StyleCodes.Duplicate]),
    ];
}
