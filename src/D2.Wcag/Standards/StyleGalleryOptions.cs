namespace D2.Wcag.Standards;

/// <summary>
/// Parametry standardu „Formatowanie z Galerii stylów” (<c>style/1.0</c>); w API bindowane z sekcji
/// <c>Wcag:StyleGallery</c>.
/// </summary>
public sealed class StyleGalleryOptions
{
    /// <summary>
    /// Właściwości formatowania bezpośredniego, których standard nie zgłasza – nazwy z <see cref="Docx.FormatProps"/>
    /// (np. <c>lang</c>, <c>noProof</c>, <c>rStyle</c>, <c>p.pStyle</c>). <c>null</c> = lista domyślna standardu;
    /// właściwość jest celowo <c>null</c>, a nie wypełniona, bo bindowanie list z konfiguracji dokleja wpisy do istniejących.
    /// </summary>
    public List<string>? IgnoredProperties { get; set; }
}
