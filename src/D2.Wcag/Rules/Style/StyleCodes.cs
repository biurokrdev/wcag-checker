namespace D2.Wcag.Rules.StyleGallery;

// Przestrzeń nazw to „StyleGallery”, nie „Style” (jak katalog): przestrzeń D2.Wcag.Rules.Style przesłoniłaby typ
// DocumentFormat.OpenXml.Wordprocessing.Style we wszystkich plikach reguł.

/// <summary>Kody zgłoszeń standardu „Formatowanie z Galerii stylów” (<c>style/1.0</c>).</summary>
public static class StyleCodes
{
    /// <summary>Formatowanie nadane ręcznie, takie samo jak wynika ze stylu.</summary>
    public const string DirectRedundant = "STY-DIRECT-REDUNDANT";

    /// <summary>Formatowanie nadane ręcznie, inne niż wynika ze stylu.</summary>
    public const string DirectOverride = "STY-DIRECT-OVERRIDE";

    /// <summary>Akapit, który wygląda jak nagłówek, ale nie ma stylu nagłówka.</summary>
    public const string FakeHeading = "STY-FAKE-HEADING";

    /// <summary>Użyty styl, którego nie ma w Galerii stylów.</summary>
    public const string NotInGallery = "STY-NOT-IN-GALLERY";

    /// <summary>Użyty styl, który jest kopią innego stylu.</summary>
    public const string Duplicate = "STY-DUPLICATE";

    /// <summary>Grupa deduplikacji wspólna z regułą hierarchii (<c>HIER-FAKE</c>): ten sam detektor, jedno zgłoszenie na akapit.</summary>
    public const string FakeHeadingGroup = "fake-heading";
}
