namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// Tekst alternatywny obiektów rysunku (kryterium 1.1.1):
/// <list type="bullet">
/// <item><c>XL-ALT-MISSING</c> (auto) – obraz albo kształt bez tekstu, który nie ma opisu i nie jest oznaczony jako dekoracyjny;</item>
/// <item><c>XL-CHART-ALT</c> (auto) – wykres bez opisu alternatywnego. Wykresy mają własny kod (inna instrukcja naprawy, lokalizacja
/// wykresu), więc <c>XL-ALT-MISSING</c> ramek wykresów nie zgłasza – jeden brak, jedno zgłoszenie;</item>
/// <item><c>XL-ALT-POOR</c> (heurystyka) – opis jest, ale nic nie mówi: nazwa pliku, nazwa obiektu, „Obraz 1”, opis bardzo krótki.</item>
/// </list>
/// Pomijane: obiekty ukryte (także w ukrytej grupie), dekoracyjne, kontrolki (osobna reguła), pola tekstowe i kształty z tekstem
/// (tekst jest treścią). Opis grupy obejmuje jej elementy; grupa bez opisu nie jest zgłaszana – oceniane są jej elementy.
/// Tytuł (<c>title</c>) liczy się jako tekst alternatywny, gdy opisu brak. Opis złożony z samych spacji to brak opisu.
/// </summary>
internal sealed class AltTextRule : IXlsxRule
{
    public const string MissingCode = "XL-ALT-MISSING";
    public const string PoorCode = "XL-ALT-POOR";
    public const string ChartCode = "XL-CHART-ALT";

    private const string CheckerNote = " Na koniec uruchom Recenzja → Sprawdź ułatwienia dostępu.";

    private const string MissingFix =
        "1. Kliknij obiekt prawym przyciskiem myszy i wybierz Wyświetl tekst alternatywny (w starszych wersjach: Edytuj tekst alternatywny). "
        + "2. W okienku wpisz jedno–dwa zdania opisujące treść i funkcję obiektu (nie jego wygląd ani nazwę pliku). "
        + "3. Jeśli obiekt jest wyłącznie ozdobą, zamiast opisu zaznacz pole Oznacz jako dekoracyjny." + CheckerNote;

    private const string PoorFix =
        "1. Kliknij obiekt prawym przyciskiem myszy i wybierz Wyświetl tekst alternatywny (w starszych wersjach: Edytuj tekst alternatywny). "
        + "2. Zastąp obecny opis jednym–dwoma zdaniami, które przekazują to samo, co obiekt: co przedstawia i po co jest w arkuszu. "
        + "Nie wpisuj nazwy pliku, nazwy obiektu ani słów „obraz”, „zdjęcie”, „wykres”. "
        + "3. Jeśli obiekt jest wyłącznie ozdobą, zaznacz pole Oznacz jako dekoracyjny." + CheckerNote;

    private const string ChartFix =
        "1. Kliknij prawym przyciskiem myszy obszar wykresu (krawędź wykresu) i wybierz Wyświetl tekst alternatywny (w starszych wersjach: "
        + "Edytuj tekst alternatywny). 2. Opisz, co wykres pokazuje: rodzaj wykresu, porównywane wielkości i najważniejszy wniosek "
        + "(trend, wartości skrajne). 3. Jeśli dane wykresu są w tabeli na arkuszu, wskaż ją w opisie (np. „dane w tabeli A1:C5”)." + CheckerNote;

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(MissingCode, "Obiekt bez tekstu alternatywnego", [Criteria.XlsxWcagCriteria.Ref("1.1.1")], XlsxCheckType.Auto, MissingFix)
        {
            Scope = "obecność tekstu alternatywnego obrazów i kształtów",
        },
        new(PoorCode, "Tekst alternatywny nie opisuje obiektu", [Criteria.XlsxWcagCriteria.Ref("1.1.1")], XlsxCheckType.Heuristic, PoorFix)
        {
            Scope = "tekst alternatywny obiektów pod kątem opisów ogólnych, nazw plików i bardzo krótkich",
            DefaultConfidence = NonTextOptions.AltGenericConfidence,
        },
        new(ChartCode, "Wykres bez opisu alternatywnego", [Criteria.XlsxWcagCriteria.Ref("1.1.1")], XlsxCheckType.Auto, ChartFix)
        {
            Scope = "obecność opisu alternatywnego wykresów",
        },
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            context.ThrowIfCancellationRequested();
            foreach (var info in NonTextRules.Objects(sheet))
            {
                var obj = info.Object;
                if (info.Hidden || obj.Type == XlsxDrawingType.Control || obj.Decorative || info.GroupDecorative)
                {
                    continue;
                }

                var chart = obj.Type == XlsxDrawingType.Chart ? sheet.Charts.FirstOrDefault(c => ReferenceEquals(c.Frame, obj)) : null;
                var location = chart is not null ? XlsxLocation.OfChart(sheet, chart) : XlsxLocation.OfDrawing(sheet, obj);
                var alt = NonTextRules.AltOf(obj);
                if (!NonTextOptions.IsBlank(alt))
                {
                    if (Poor(obj, alt!.Trim()) is var (reason, confidence))
                    {
                        yield return new XlsxRuleHit(PoorCode,
                            $"Tekst alternatywny obiektu „{obj.Name}” ({NonTextRules.TypeLabel(obj)}) prawdopodobnie nie opisuje jego treści: {reason}.",
                            location)
                        {
                            Confidence = confidence,
                            Evidence = Evidence(obj, alt.Trim()),
                        };
                    }

                    continue;
                }

                if (info.GroupDescribed || obj.IsGroup)
                {
                    continue;
                }

                if (obj.Type == XlsxDrawingType.Chart)
                {
                    if (sheet.IsChartSheet)
                    {
                        yield return new XlsxRuleHit(ChartCode,
                            $"Wykres na arkuszu wykresu „{sheet.Name}” nie ma opisu alternatywnego. Sprawdź, czy nazwa arkusza i tytuł wykresu wystarczają, by zrozumieć, co wykres przedstawia.",
                            location)
                        {
                            CheckType = XlsxCheckType.Heuristic,
                            Confidence = NonTextOptions.ChartSheetAltConfidence,
                            Evidence = Evidence(obj, null),
                        };
                    }
                    else
                    {
                        yield return new XlsxRuleHit(ChartCode,
                            $"Wykres „{obj.Name}” nie ma opisu alternatywnego – osoba korzystająca z czytnika ekranu nie dowie się, co wykres przedstawia.",
                            location)
                        {
                            Evidence = Evidence(obj, null),
                        };
                    }

                    continue;
                }

                if (obj.Type == XlsxDrawingType.Image || NonTextOptions.IsBlank(obj.Text))
                {
                    yield return new XlsxRuleHit(MissingCode,
                        $"Obiekt „{obj.Name}” ({NonTextRules.TypeLabel(obj)}) nie ma tekstu alternatywnego i nie jest oznaczony jako dekoracyjny.",
                        location)
                    {
                        Evidence = Evidence(obj, null),
                    };
                }
            }
        }
    }

    /// <summary>Jakość i trafność istniejących opisów ocenia człowiek – pytanie zostaje, gdy skoroszyt ma jakiekolwiek obiekty graficzne.</summary>
    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        var hasObjects = workbook.HasEmbeddedMedia
            || workbook.Sheets.Any(s => NonTextRules.Objects(s).Any(o => !o.Hidden && o.Object.Type != XlsxDrawingType.Control));
        if (hasObjects)
        {
            yield return NonTextRules.CriterionCheck("1.1.1");
        }
    }

    private static (string Reason, double Confidence)? Poor(XlsxDrawingObject obj, string alt)
    {
        if (NonTextOptions.EndsWithImageExtension(alt)
            || (obj.ImageFileName is { Length: > 0 } file && alt.Equals(file, StringComparison.OrdinalIgnoreCase)))
        {
            return ("opis jest nazwą pliku", NonTextOptions.AltFileNameConfidence);
        }

        if (obj.Name.Length > 0 && NonTextOptions.Normalize(alt) == NonTextOptions.Normalize(obj.Name))
        {
            return ("opis jest taki sam jak nazwa obiektu", NonTextOptions.AltGenericConfidence);
        }

        if (NonTextOptions.IsGenericAlt(alt))
        {
            return ("opis to słowo ogólne, które nie mówi, co obiekt przedstawia", NonTextOptions.AltGenericConfidence);
        }

        if (NonTextOptions.IsGeneratedAlt(alt))
        {
            return ("opis został wygenerowany automatycznie i wymaga sprawdzenia", NonTextOptions.AltGeneratedConfidence);
        }

        if (alt.Length < NonTextOptions.AltMinLength)
        {
            return ($"opis ma mniej niż {NonTextOptions.AltMinLength} znaków", NonTextOptions.AltShortConfidence);
        }

        return null;
    }

    private static List<XlsxEvidence> Evidence(XlsxDrawingObject obj, string? alt)
    {
        var evidence = new List<XlsxEvidence>
        {
            new("Typ obiektu", NonTextRules.TypeLabel(obj)),
            new("Nazwa obiektu", obj.Name),
        };
        if (alt is not null)
        {
            evidence.Add(new(NonTextOptions.IsBlank(obj.Description) ? "Obecny tytuł (pole opisu jest puste)" : "Obecny opis", alt));
        }
        else
        {
            evidence.Add(new("Obecny opis", obj.Description is null ? "(brak)" : "(pusty)"));
        }

        if (obj.ImageFileName is { Length: > 0 } file)
        {
            evidence.Add(new("Plik obrazu", file));
        }

        if (obj.HyperlinkTarget is { Length: > 0 } target)
        {
            evidence.Add(new("Łącze (obiekt jest klikalny)", target));
        }

        return evidence;
    }
}
