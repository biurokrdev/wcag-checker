using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

/// <summary>
/// Styl z styles.xml: identyfikator, surowa nazwa (w:name), nazwa jak w polskim Wordzie, rodzaj, styl bazowy
/// i połączony, oraz to, jak Word go pokazuje. <paramref name="InGallery"/> = w:qFormat (Galeria stylów),
/// <paramref name="IsCustom"/> = w:customStyle (styl użytkownika, nie wbudowany).
/// </summary>
public sealed record StyleInfo(string Id, string? Name, string DisplayName, StyleValues Type, string? BasedOn,
    string? LinkedStyleId, bool IsDefault, bool IsCustom, bool InGallery, bool SemiHidden,
    bool UnhideWhenUsed, int? UiPriority)
{
    internal static StyleInfo From(Style style)
    {
        var id = style.StyleId!.Value!;
        var name = style.StyleName?.Val?.Value;
        return new StyleInfo(
            id,
            name,
            StyleDisplayNames.Of(name, id),
            style.Type?.Value ?? StyleValues.Paragraph,
            style.BasedOn?.Val?.Value,
            style.LinkedStyle?.Val?.Value,
            style.Default?.Value ?? false,
            style.CustomStyle?.Value ?? false,
            IsOn(style.PrimaryStyle),
            IsOn(style.SemiHidden),
            IsOn(style.UnhideWhenUsed),
            style.UIPriority?.Val?.Value);
    }

    private static bool IsOn(OnOffOnlyType? element) => element is not null && (element.Val is null || element.Val.Value == OnOffOnlyValues.On);
}
