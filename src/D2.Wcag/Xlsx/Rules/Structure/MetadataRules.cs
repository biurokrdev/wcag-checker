using System.Globalization;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// XL-TITLE (2.4.2): tytuł we właściwościach dokumentu. Brak tytułu (także pusty albo same białe znaki) to w profilu 2.2
/// niespełnienie kryterium w brzmieniu zalecanym przez WCAG2ICT (kontrola automatyczna); w profilu 2.1 norma cytowana
/// (EN 301 549 V3.2.1, 10.2.4.2 NOTE 1) dopuszcza opisową nazwę pliku, więc wynik jest „do weryfikacji”. Tytuł domyślny
/// albo równy nazwie pliku to w obu profilach heurystyka (macierz, rozdz. 4).
/// </summary>
internal sealed class TitleRule : IXlsxRule
{
    public const string Code = "XL-TITLE";

    private const string Fix =
        "1. Otwórz Plik → Informacje. 2. W panelu Właściwości kliknij pole Tytuł (jeśli go nie widać: Właściwości → Właściwości zaawansowane → karta Podsumowanie). "
        + "3. Wpisz tytuł opisujący nazwę, temat lub cel skoroszytu, np. „Budżet działu na 2026 rok”. 4. Zapisz plik.";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Brak opisowego tytułu skoroszytu we właściwościach dokumentu",
        [XlsxWcagCriteria.Ref("2.4.2")],
        XlsxCheckType.Auto,
        Fix)
    {
        Scope = "tytuł we właściwościach dokumentu",
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        var title = workbook.Properties.Title?.Trim() ?? "";
        if (title.Length == 0)
        {
            if (context.Profile.WcagVersion == XlsxAuditProfile.Wcag21)
            {
                yield return new XlsxRuleHit(Code,
                    "Skoroszyt nie ma tytułu we właściwościach dokumentu. W profilu WCAG 2.1 norma EN 301 549 V3.2.1 (klauzula 10.2.4.2, NOTE 1) uznaje za tytuł także opisową nazwę pliku – sprawdź, czy nazwa pliku opisuje temat lub cel skoroszytu, albo uzupełnij tytuł.",
                    XlsxLocation.Workbook())
                {
                    CheckType = XlsxCheckType.Heuristic,
                    Confidence = StructureOptions.TitleMissingIn21Confidence,
                    Evidence = Evidence(workbook, context),
                };
            }
            else
            {
                yield return new XlsxRuleHit(Code,
                    "Skoroszyt nie ma tytułu we właściwościach dokumentu. To niespełnienie kryterium 2.4.2 w brzmieniu zalecanym przez WCAG2ICT dla dokumentów nie-webowych (odpowiednik: EN 301 549 V4.1.1, klauzula 10.2.4.2): dokument ma mieć tytuł opisujący nazwę, temat lub cel.",
                    XlsxLocation.Workbook())
                {
                    Evidence = Evidence(workbook, context),
                };
            }

            yield break;
        }

        if (StructureOptions.DefaultTitle.IsMatch(title))
        {
            yield return new XlsxRuleHit(Code,
                $"Tytuł skoroszytu „{XlsxLocation.Shorten(title)}” wygląda na domyślny i nie opisuje tematu ani celu skoroszytu. Możliwy problem z kryterium 2.4.2 – wymaga weryfikacji przez człowieka.",
                XlsxLocation.Workbook())
            {
                CheckType = XlsxCheckType.Heuristic,
                Confidence = StructureOptions.TitleDefaultConfidence,
                Evidence = Evidence(workbook, context),
            };
            yield break;
        }

        if (EqualsFileName(title, context.Profile.FileName))
        {
            yield return new XlsxRuleHit(Code,
                $"Tytuł skoroszytu „{XlsxLocation.Shorten(title)}” jest taki sam jak nazwa pliku. Sprawdź, czy opisuje temat lub cel skoroszytu (jeśli nazwa pliku jest opisowa, tytuł jest poprawny).",
                XlsxLocation.Workbook())
            {
                CheckType = XlsxCheckType.Heuristic,
                Confidence = StructureOptions.TitleEqualsFileNameConfidence,
                Evidence = Evidence(workbook, context),
            };
        }
    }

    /// <summary>Tytuł równy nazwie pliku – z rozszerzeniem albo bez, bez rozróżniania wielkości liter; katalogi są pomijane.</summary>
    internal static bool EqualsFileName(string title, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var name = fileName.Trim();
        var separator = name.LastIndexOfAny(['/', '\\']);
        if (separator >= 0)
        {
            name = name[(separator + 1)..];
        }

        if (name.Length == 0)
        {
            return false;
        }

        if (string.Equals(title, name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var dot = name.LastIndexOf('.');
        return dot > 0 && string.Equals(title, name[..dot].Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static List<XlsxEvidence> Evidence(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        var title = workbook.Properties.Title;
        var evidence = new List<XlsxEvidence>
        {
            new("Tytuł we właściwościach", title is null ? "brak" : string.IsNullOrWhiteSpace(title) ? "pusty" : "„" + XlsxLocation.Shorten(title.Trim()) + "”"),
        };
        if (!string.IsNullOrWhiteSpace(context.Profile.FileName))
        {
            evidence.Add(new XlsxEvidence("Nazwa pliku", context.Profile.FileName.Trim()));
        }

        return evidence;
    }
}

/// <summary>
/// XL-LANG (3.1.1): brak właściwości języka dokumentu. Nie potwierdzono w źródłach, że Excel i czytniki ekranu z niej
/// korzystają, a Excel często jej nie zapisuje – dlatego wynik jest wyłącznie „do weryfikacji”: reguła nie podaje
/// pewności, więc żaden próg ostrzeżenia nie zrobi z niej ostrzeżenia (kontrakt pkt 2.5).
/// </summary>
internal sealed class LanguageRule : IXlsxRule
{
    public const string Code = "XL-LANG";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Brak informacji o języku skoroszytu we właściwościach dokumentu",
        [XlsxWcagCriteria.Ref("3.1.1")],
        XlsxCheckType.Heuristic,
        "Excel nie ma w oknie właściwości pola języka dokumentu, więc samego braku nie da się tam uzupełnić. 1. Otwórz skoroszyt z czytnikiem ekranu (np. NVDA albo Narrator) i sprawdź, czy treść komórek i obiektów jest czytana w języku, w którym została napisana. "
        + "2. Sprawdź języki tworzenia i sprawdzania pakietu Office: Plik → Opcje → Język. "
        + "3. Jeśli skoroszyt powstaje w narzędziu, które zapisuje właściwości pliku (eksport, generator raportów), ustaw w nim język dokumentu (np. pl-PL).")
    {
        Scope = "język we właściwościach dokumentu",
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        if (string.IsNullOrWhiteSpace(workbook.Properties.Language))
        {
            yield return new XlsxRuleHit(Code,
                "Właściwości dokumentu nie zawierają języka skoroszytu. Excel często nie zapisuje tej właściwości i nie potwierdzono, że czytniki ekranu z niej korzystają – to nie jest stwierdzenie niespełnienia kryterium 3.1.1, tylko sygnał do sprawdzenia, czy treść jest odczytywana we właściwym języku.",
                XlsxLocation.Workbook())
            {
                Evidence = [new XlsxEvidence("Język we właściwościach", workbook.Properties.Language is null ? "brak" : "pusty")],
            };
        }
    }
}

/// <summary>
/// XL-SHEET-NAME (2.4.6): nazwa arkusza domyślna („Arkusz1”, „Sheet2”) albo 1–2-znakowa. Nazwę arkusza traktujemy jak
/// etykietę (interpretacja; podstawa praktyczna: reguła Microsoft „Sheet tabs have meaningful names”). Kryterium 2.4.2
/// dotyczy tytułu dokumentu, nie arkuszy – nie jest tu przypisane.
/// <para>Arkusze ukryte i bardzo ukryte są pomijane: ich kart użytkownik nie widzi, a samo ukrycie zgłasza XL-HIDDEN. Pomijane są też
/// arkusze puste – nazwa nie ma czego opisywać (np. jedyny arkusz nowego skoroszytu).</para>
/// </summary>
internal sealed class SheetNameRule : IXlsxRule
{
    public const string Code = "XL-SHEET-NAME";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Nazwa arkusza nie opisuje jego zawartości",
        [XlsxWcagCriteria.Ref("2.4.6")],
        XlsxCheckType.Heuristic,
        "1. Kliknij prawym przyciskiem myszy kartę arkusza u dołu okna (albo naciśnij Shift+F10 na karcie). 2. Wybierz Zmień nazwę. "
        + "3. Wpisz krótką nazwę opisującą zawartość arkusza, np. „Sprzedaż 2026”, i naciśnij Enter. Wynik sprawdzisz poleceniem Recenzja → Sprawdź ułatwienia dostępu.")
    {
        Scope = "nazwy arkuszy",
        DefaultConfidence = StructureOptions.SheetNameDefaultConfidence,
        HasSubject = w => w.Sheets.Any(Examined),
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    private static bool Examined(XlsxSheet sheet) => sheet.State == XlsxSheetState.Visible && !sheet.IsEmpty;

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            if (!Examined(sheet))
            {
                continue;
            }

            var name = sheet.Name.Trim();
            if (StructureOptions.DefaultSheetName.IsMatch(name))
            {
                yield return new XlsxRuleHit(Code,
                    $"Arkusz ma domyślną nazwę „{sheet.Name}”, która nie opisuje jego zawartości. Możliwy problem z kryterium 2.4.6 – wymaga weryfikacji przez człowieka.",
                    XlsxLocation.OfSheet(sheet))
                {
                    Confidence = StructureOptions.SheetNameDefaultConfidence,
                    Evidence = [new XlsxEvidence("Nazwa arkusza", sheet.Name), new XlsxEvidence("Rodzaj nazwy", "domyślna")],
                };
            }
            else if (new StringInfo(name).LengthInTextElements <= StructureOptions.ShortSheetNameMaxLength)
            {
                yield return new XlsxRuleHit(Code,
                    $"Nazwa arkusza „{sheet.Name}” ma najwyżej {StructureOptions.ShortSheetNameMaxLength} znaki – sprawdź, czy opisuje zawartość arkusza (kryterium 2.4.6).",
                    XlsxLocation.OfSheet(sheet))
                {
                    Confidence = StructureOptions.SheetNameShortConfidence,
                    Evidence = [new XlsxEvidence("Nazwa arkusza", sheet.Name), new XlsxEvidence("Rodzaj nazwy", "bardzo krótka")],
                };
            }
        }
    }
}
