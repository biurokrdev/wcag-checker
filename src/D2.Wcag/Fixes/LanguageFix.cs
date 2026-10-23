using D2.Wcag.Docx;
using D2.Wcag.Rules;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Fixes;

// Język trafia w dwa miejsca: do właściwości pakietu (dc:language) i do domyślnych ustawień stylów (w:docDefaults/w:lang),
// bo to drugie jest językiem sprawdzania i odczytu w Wordzie. Brakujący albo niepoprawny (np. „Polish”) wpis dostaje
// WcagFixOptions.Language (fixer wstawia domyślny z profilu).
internal sealed class LanguageFix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.LanguageMissing, IssueCodes.LanguageInvalid];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var language = string.IsNullOrWhiteSpace(options.Language) ? "pl-PL" : options.Language.Trim();
        var changed = false;

        var properties = document.Package.PackageProperties;
        if (string.IsNullOrWhiteSpace(properties.Language) || !MetadataRules.IsValidLanguageTag(properties.Language))
        {
            properties.Language = language;
            changed = true;
        }

        var styles = document.Main.StyleDefinitionsPart?.Styles;
        if (styles is not null)
        {
            var docDefaults = styles.DocDefaults ??= new DocDefaults();
            var runDefaults = docDefaults.RunPropertiesDefault ??= new RunPropertiesDefault();
            var baseStyle = runDefaults.RunPropertiesBaseStyle ??= new RunPropertiesBaseStyle();
            var languages = baseStyle.Languages ??= new Languages();
            if (string.IsNullOrWhiteSpace(languages.Val?.Value) || !MetadataRules.IsValidLanguageTag(languages.Val!.Value!))
            {
                languages.Val = language;
                changed = true;
            }
        }

        if (changed)
        {
            yield return new AppliedFix(IssueCodes.LanguageMissing, $"Ustawiono język dokumentu „{language}”.");
        }
    }
}
