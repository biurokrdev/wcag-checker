using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// <c>XL-CONTROL</c> (4.1.2). Macierz (rozdz. 4): automatycznie da się stwierdzić tylko brak nazwy kontrolki; obsługa z klawiatury
/// (2.1.1, 2.1.2) jest wyłącznie do oceny ręcznej.
/// <list type="bullet">
/// <item>Kontrolka formularza bez etykiety (podpisu) i bez tekstu alternatywnego → zgłoszenie automatyczne (błąd).</item>
/// <item>Kontrolka ActiveX → zawsze pozycja checklisty powiązana z 4.1.2, 2.1.1 i 2.1.2 (zawartości ActiveX parser nie czyta).</item>
/// </list>
/// </summary>
internal sealed class ControlRule : IXlsxRule
{
    public const string Code = "XL-CONTROL";

    private const string Fix =
        "1. Kliknij kontrolkę prawym przyciskiem myszy i wybierz Formatuj kontrolkę. 2. Na karcie Tekst alternatywny wpisz krótką nazwę mówiącą, "
        + "do czego kontrolka służy (np. „Zgoda na przetwarzanie danych”), i zatwierdź OK. 3. Dla pola wyboru, przycisku opcji i przycisku możesz też "
        + "wpisać widoczny podpis: prawy przycisk myszy → Edytuj tekst. 4. Uruchom Recenzja → Sprawdź ułatwienia dostępu.";

    private const string ActiveXHint =
        "Kontrolek ActiveX audyt nie analizuje. Sprawdź z czytnikiem ekranu i samą klawiaturą: czy kontrolka ma odczytywaną nazwę, rolę i stan, "
        + "czy da się do niej dojść i ją obsłużyć oraz czy da się z niej wyjść (np. klawiszem Esc). Jeśli nie – zastąp ją kontrolką formularza "
        + "(Deweloper → Wstaw → Kontrolki formularza) albo listą z poprawności danych (Dane → Poprawność danych).";

    /// <summary>Rodzaje kontrolek formularza (<see cref="XlsxControl.Kind"/>), dla których brak nazwy zgłaszamy automatycznie.</summary>
    private static readonly HashSet<string> FormControlKinds = new(StringComparer.Ordinal)
    {
        "CheckBox", "Button", "ComboBox", "ListBox", "OptionButton", "GroupBox", "Label", "ScrollBar", "Spinner", "EditBox",
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(Code, "Kontrolka bez nazwy", [XlsxWcagCriteria.Ref("4.1.2")], XlsxCheckType.Auto, Fix)
        {
            Scope = "nazwy (etykiety i tekst alternatywny) kontrolek formularza",
            HasSubject = w => w.Sheets.Any(s => s.Controls.Any(c => !c.IsActiveX)),
        },
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            foreach (var control in sheet.Controls)
            {
                // Rodzaj nierozpoznany (np. kształt VML typu „Pict”) nie daje pewności, że to kontrolka formularza – bez zgłoszenia automatycznego.
                if (control.IsActiveX || !FormControlKinds.Contains(control.Kind) || !NonTextOptions.IsBlank(control.Label)
                    || !NonTextOptions.IsBlank(control.AltText))
                {
                    continue;
                }

                var evidence = new List<XlsxEvidence>
                {
                    new("Rodzaj kontrolki", KindLabel(control.Kind)),
                    new("Nazwa obiektu", control.Name),
                    new("Etykieta", "(brak)"),
                    new("Tekst alternatywny", "(brak)"),
                };
                if (control.LinkedCell is { Length: > 0 } linked)
                {
                    evidence.Add(new("Komórka połączona", linked));
                }

                yield return new XlsxRuleHit(Code,
                    $"Kontrolka formularza „{control.Name}” ({KindLabel(control.Kind)}) nie ma etykiety ani tekstu alternatywnego – czytnik ekranu nie poda jej nazwy.",
                    XlsxLocation.OfControl(sheet, control))
                {
                    Evidence = evidence,
                };
            }
        }
    }

    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        var any = false;
        foreach (var sheet in workbook.Sheets)
        {
            foreach (var control in sheet.Controls)
            {
                any = true;
                if (!control.IsActiveX)
                {
                    continue;
                }

                var location = XlsxLocation.OfControl(sheet, control);
                yield return new XlsxManualCheck
                {
                    Id = Code + ":" + location.Key,
                    Wcag = [XlsxWcagCriteria.Ref("4.1.2"), XlsxWcagCriteria.Ref("2.1.1"), XlsxWcagCriteria.Ref("2.1.2")],
                    Question = $"Czy kontrolka ActiveX „{control.Name}” ma nazwę, rolę i stan odczytywane przez czytnik ekranu, da się ją obsłużyć samą klawiaturą i da się z niej wyjść klawiaturą?",
                    Hint = ActiveXHint,
                    Location = location,
                    Code = Code,
                };
            }
        }

        // Rolę i stan ogłasza oprogramowanie, a trafność nazwy ocenia człowiek – pytanie zostaje, gdy są kontrolki albo klikalne obiekty.
        if (any || workbook.Sheets.Any(s => s.Drawings.Any(d => d.HyperlinkTarget is not null)))
        {
            yield return NonTextRules.CriterionCheck("4.1.2");
        }
    }

    private static string KindLabel(string kind) => kind switch
    {
        "CheckBox" => "pole wyboru",
        "Button" => "przycisk",
        "ComboBox" => "pole kombi (lista rozwijana)",
        "ListBox" => "pole listy",
        "OptionButton" => "przycisk opcji",
        "GroupBox" => "pole grupy",
        "Label" => "etykieta",
        "ScrollBar" => "pasek przewijania",
        "Spinner" => "przycisk pokrętła",
        "EditBox" => "pole edycji",
        _ => "kontrolka formularza",
    };
}
