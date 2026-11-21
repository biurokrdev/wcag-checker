using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// Hiperłącza. Adres docelowy jest tylko tekstem dowodu – reguła nigdy nie otwiera łączy.
/// <list type="bullet">
/// <item><c>XL-LINK-TEXT</c> (2.4.4, heurystyka) – tekst łącza nie mówi, dokąd prowadzi: tekst ogólny („kliknij tutaj”) daje
/// „do weryfikacji” (kryterium dopuszcza cel wynikający z kontekstu); łącze na pustej komórce i klikalny obiekt oznaczony jako
/// dekoracyjny – ostrzeżenie (łącze nie ma żadnej nazwy). Klikalnego obiektu bez opisu reguła nie dubluje – zgłasza go
/// <c>XL-ALT-MISSING</c>.</item>
/// <item><c>XL-LINK-URL</c> (dobra praktyka, bez SC) – tekst łącza jest surowym adresem; to zalecenie Microsoft, nie niespełnienie
/// 2.4.4 (macierz rozdz. 4).</item>
/// </list>
/// Tekst łącza to tekst komórki; gdy komórka jest pusta – atrybut <c>display</c>. Formuły bez wartości zapisanej w pliku są pomijane
/// (tekstu nie da się ustalić bez przeliczania).
/// </summary>
internal sealed class LinkRule : IXlsxRule
{
    public const string TextCode = "XL-LINK-TEXT";
    public const string UrlCode = "XL-LINK-URL";

    private const string TextFix =
        "1. Kliknij komórkę z łączem prawym przyciskiem myszy i wybierz Edytuj hiperlink (w nowszych wersjach: Edytuj link). "
        + "2. W polu Tekst do wyświetlenia wpisz tekst, który sam mówi, dokąd prowadzi łącze, np. „Regulamin budżetowania (PDF)” zamiast „kliknij tutaj”. "
        + "3. Opcjonalnie dodaj wskazówkę przyciskiem Etykietka ekranowa. Jeśli łącze jest na obrazie lub kształcie, wpisz cel łącza w jego tekście "
        + "alternatywnym (prawy przycisk myszy → Wyświetl tekst alternatywny) i nie oznaczaj obiektu jako dekoracyjny.";

    private const string UrlFix =
        "1. Kliknij komórkę z łączem prawym przyciskiem myszy i wybierz Edytuj hiperlink (w nowszych wersjach: Edytuj link). "
        + "2. W polu Tekst do wyświetlenia zastąp adres krótkim opisem celu, np. „Raport kwartalny Q1 2026”; adres zostaje w polu Adres. "
        + "Długi adres odczytany przez czytnik ekranu jest mało zrozumiały, dlatego opisowy tekst jest wygodniejszy.";

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(TextCode, "Tekst łącza nie wskazuje celu", [XlsxWcagCriteria.Ref("2.4.4")], XlsxCheckType.Heuristic, TextFix)
        {
            Scope = "tekst hiperłączy i łącza w obiektach",
            DefaultConfidence = NonTextOptions.LinkGenericConfidence,
        },
        new(UrlCode, "Tekst łącza jest surowym adresem", [], XlsxCheckType.Auto, UrlFix) { BestPractice = true },
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            context.ThrowIfCancellationRequested();
            foreach (var link in sheet.Hyperlinks)
            {
                var cell = sheet.Cell(link.Ref.From);
                if (cell is { HasFormula: true, HasValue: false })
                {
                    continue;
                }

                var text = NonTextOptions.IsBlank(link.CellText) ? (link.Display ?? "").Trim() : link.CellText.Trim();
                var location = XlsxLocation.OfRange(sheet, link.Ref, text);
                if (text.Length == 0)
                {
                    yield return new XlsxRuleHit(TextCode,
                        "Łącze nie ma tekstu – komórka z łączem jest pusta, więc nie wiadomo, dokąd łącze prowadzi.", location)
                    {
                        Confidence = NonTextOptions.LinkEmptyConfidence,
                        Evidence = Evidence("(pusty)", link),
                    };
                }
                else if (NonTextOptions.IsGenericLinkText(text))
                {
                    yield return new XlsxRuleHit(TextCode,
                        $"Tekst łącza „{text}” nie mówi, dokąd łącze prowadzi. Sprawdź, czy cel wynika z nagłówków wiersza i kolumny; jeśli nie, zmień tekst.",
                        location)
                    {
                        Confidence = NonTextOptions.LinkGenericConfidence,
                        Evidence = Evidence(text, link),
                    };
                }
                else if (NonTextOptions.IsRawUrl(text))
                {
                    yield return new XlsxRuleHit(UrlCode, "Tekst łącza jest surowym adresem – lepszy jest krótki opis celu.", location)
                    {
                        Evidence = Evidence(text, link),
                    };
                }
            }

            foreach (var info in NonTextRules.Objects(sheet))
            {
                var obj = info.Object;
                if (info.Hidden || obj.HyperlinkTarget is null || obj.Type == XlsxDrawingType.Control
                    || !(obj.Decorative || info.GroupDecorative) || !NonTextOptions.IsBlank(obj.Text))
                {
                    continue;
                }

                yield return new XlsxRuleHit(TextCode,
                    $"Obiekt „{obj.Name}” ({NonTextRules.TypeLabel(obj)}) jest łączem, a jest oznaczony jako dekoracyjny – łącze nie ma nazwy, którą odczyta czytnik ekranu.",
                    XlsxLocation.OfDrawing(sheet, obj))
                {
                    Confidence = NonTextOptions.LinkDecorativeObjectConfidence,
                    Evidence = [new("Tekst łącza", "(brak – obiekt dekoracyjny)"), new("Adres docelowy", obj.HyperlinkTarget)],
                };
            }
        }
    }

    /// <summary>Czy cel łącza wynika z tekstu i kontekstu, ocenia człowiek – pytanie zostaje, gdy skoroszyt ma łącza.</summary>
    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        if (workbook.Sheets.Any(s => s.Hyperlinks.Count > 0 || s.Drawings.Any(d => d.HyperlinkTarget is not null)))
        {
            yield return NonTextRules.CriterionCheck("2.4.4");
        }
    }

    private static List<XlsxEvidence> Evidence(string text, XlsxHyperlink link)
    {
        var evidence = new List<XlsxEvidence> { new("Tekst łącza", text) };
        if (link.Target is { Length: > 0 } target)
        {
            evidence.Add(new("Adres docelowy", target));
        }

        if (link.Location is { Length: > 0 } place)
        {
            evidence.Add(new("Miejsce w dokumencie", place));
        }

        if (link.Tooltip is { Length: > 0 } tooltip)
        {
            evidence.Add(new("Etykietka ekranowa", tooltip));
        }

        return evidence;
    }
}
