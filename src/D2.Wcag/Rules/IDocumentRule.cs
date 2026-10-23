using D2.Wcag.Docx;

namespace D2.Wcag.Rules;

/// <summary>
/// Jedna grupa sprawdzeń. Reguła tylko czyta dokument i zwraca zgłoszenia; parametry (progi, wymagane wartości) dostaje
/// w konstruktorze od standardu, który ją tworzy. Własne reguły implementują ten interfejs i trafiają do własnego
/// <see cref="Standards.IDocumentStandard"/>.
/// </summary>
public interface IDocumentRule
{
    IEnumerable<WcagIssue> Check(DocxDocument document);
}

internal static class Units
{
    public const double TwipsPerPt = 20.0;

    public static string Pt(double value) => value.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("pl-PL"));

    public static string Mm(double twips) => (twips / TwipsPerPt * 25.4 / 72).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
}
