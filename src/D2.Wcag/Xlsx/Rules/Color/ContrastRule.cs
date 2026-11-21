namespace D2.Wcag.Xlsx.Rules.Color;

/// <summary>
/// XL-CONTRAST (1.4.3): kontrast tekstu komórki względem jej tła poniżej 4,5:1, a dla dużego tekstu (od 18 pt albo od 14 pt
/// pogrubionego) poniżej 3:1 – porównanie na wartościach niezaokrąglonych.
/// <para>
/// Kontrola automatyczna (błąd) tylko dla par jawnych i pewnych: kolor czcionki i tło zapisane jako RGB albo indeks palety,
/// przy czym brak wypełnienia oznacza białe tło (WCAG, „contrast ratio”, Note 3). Heurystyka, gdy którykolwiek kolor jest
/// kolorem motywu albo ma odcień (wartość przeliczana), jest automatyczny lub systemowy, tło jest wzorem albo gradientem,
/// komórka leży w tabeli ze stylem tabeli (i nie ma własnego wypełnienia albo ma automatyczny kolor czcionki) albo zakres ma
/// formatowanie warunkowe zmieniające kolory. Tekst w kolorze tła (1:1) to osobny komunikat „do weryfikacji”: może być treścią
/// ukrytą celowo, której wyjątek „not visible to anyone” z 1.4.3 nie obejmuje wymaganiem kontrastu.
/// </para>
/// <para>
/// Oceniane są tylko komórki z widoczną treścią: pomijane są komórki puste i z samymi odstępami, formuły bez zapisanej
/// wartości, treść ukryta formatem liczbowym (np. „;;;”), arkusze ukryte oraz ukryte wiersze i kolumny.
/// </para>
/// </summary>
internal sealed class ContrastRule : IXlsxRule
{
    public const string Code = "XL-CONTRAST";

    /// <summary>Pytanie checklisty 1.4.3 o kontrast, którego narzędzie nie ocenia (style tabel, obrazy tła, obiekty, tło przybliżone).</summary>
    internal const string ManualId = "manual-1.4.3:color";

    /// <summary>Pytanie checklisty o wyjątki z 1.4.3 (nieaktywne, dekoracyjne, logotypy, treść niewidoczna).</summary>
    internal const string ExceptionsId = "manual-1.4.3:exceptions";

    private const string BaseFix =
        "Zmień kolor czcionki albo wypełnienia tak, aby kontrast wynosił co najmniej 4,5:1 (3:1 dla tekstu od 18 pt albo od 14 pt pogrubionego): "
        + "zaznacz komórki i użyj Narzędzia główne → Kolor czcionki albo Narzędzia główne → Kolor wypełnienia. "
        + "Wynik sprawdź w Recenzja → Sprawdź ułatwienia dostępu.";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Za niski kontrast tekstu względem tła komórki",
        [Criteria.XlsxWcagCriteria.Ref("1.4.3")],
        XlsxCheckType.Auto,
        BaseFix)
    {
        Scope = "kontrast tekstu w komórkach",
        HasSubject = XlsxSubjects.HasVisibleCellValues,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context) =>
        ColorAnalysis.For(workbook, context).ContrastHits;

    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        var analysis = ColorAnalysis.For(workbook, context);
        var wcag = (IReadOnlyList<WcagRef>)[context.Ref("1.4.3")];
        var visible = workbook.Sheets.Where(s => s.State == XlsxSheetState.Visible).ToList();

        if (analysis.ContrastHits.Count > 0 || ConditionalContrastRule.HasFindings(workbook, context))
        {
            yield return new XlsxManualCheck
            {
                Id = ExceptionsId,
                Wcag = wcag,
                Question = "Czy teksty zgłoszone jako mające za niski kontrast są objęte wyjątkiem z kryterium 1.4.3: należą do nieaktywnego elementu, są czystą dekoracją, logotypem albo treścią celowo niewidoczną? Narzędzie nie rozpoznaje tych wyjątków – pozostałe zgłoszone teksty wymagają poprawy kolorów.",
                Hint = "Przejrzyj zgłoszenia XL-CONTRAST i XL-CONTRAST-CF. Tekst, który ma być czytany, musi mieć kontrast co najmniej 4,5:1 (duży tekst 3:1).",
                Code = Code,
            };
        }

        // Jedno pytanie na kryterium (macierz, pkt 6.1), zadawane tylko wtedy, gdy w skoroszycie jest treść, której kontrastu
        // narzędzie nie ocenia albo ocenia w przybliżeniu; wykryte miejsca wymienia wskazówka.
        var found = new List<string>();
        if (visible.Any(s => s.Tables.Any(t => t.StyleName is not null)))
        {
            found.Add("tabele ze stylem tabeli (wiersz nagłówka, pasy wierszy i kolumn, wiersz sumy) – zaznacz komórkę tabeli i sprawdź Projekt tabeli → Style tabeli");
        }

        if (visible.Any(s => s.Charts.Count > 0 || s.Drawings.Any(d => !d.Hidden && d.Text.Trim().Length > 0)))
        {
            found.Add("tekst w kształtach, polach tekstowych i na wykresach (tytuły, etykiety osi i danych, legenda) – zaznacz obiekt i sprawdź Formatuj kształt → Opcje tekstu → Wypełnienie tekstu oraz wypełnienie kształtu lub obszaru wykresu");
        }

        // Obraz może zawierać tekst (obraz tekstu), którego kontrastu bez OCR nie da się ocenić – bez tego pytania 1.4.3
        // dostałby „Brak wykrytych problemów”, choć obrazów nikt nie sprawdził.
        if (visible.Any(s => s.Drawings.Any(d => d.Type == XlsxDrawingType.Image && !d.Hidden && !d.Decorative)))
        {
            found.Add("obrazy, które mogą zawierać tekst (zrzuty ekranu, napisy, tabele jako obraz) – kontrast tekstu w obrazie oceń na samym obrazie, np. narzędziem do pomiaru kontrastu kolorów");
        }

        if (analysis.HasTextOnApproximateBackground)
        {
            found.Add("komórki z wypełnieniem deseniem albo gradientem, ocenione w przybliżeniu – sprawdź Formatuj komórki → Wypełnienie (Kolor deseniu, Styl deseniu, Efekty wypełnienia)");
        }

        if (analysis.HasAutomaticTextOnFill)
        {
            found.Add("tekst w automatycznym kolorze czcionki na kolorowym wypełnieniu – sprawdź Narzędzia główne → Kolor czcionki");
        }

        if (found.Count > 0)
        {
            yield return new XlsxManualCheck
            {
                Id = ManualId,
                Wcag = wcag,
                Question = "Czy tekst na tle z obrazu, gradientu, wzoru lub w kolorach automatycznych, a także tekst w tabelach ze stylem tabeli, w kształtach, na wykresach i w obrazach (obrazy tekstu), jest wyraźnie czytelny (kontrast co najmniej 4,5:1, dla dużego tekstu 3:1)?",
                Hint = "Narzędzie ocenia kontrast tylko z formatowania komórek; nie ocenia stylów tabel, obrazów tła arkusza (Układ strony → Tło), tekstu w obiektach ani tekstu w obrazach. W tym skoroszycie: "
                    + string.Join("; ", found) + ". Kolory oceń też w Recenzja → Sprawdź ułatwienia dostępu.",
                Code = Code,
            };
        }
    }

    internal static string Message(ContrastCase contrastCase)
    {
        var subject = contrastCase.Fragment is null ? "Tekst" : $"Fragment tekstu „{contrastCase.Fragment}”";
        if (contrastCase.IsInvisible)
        {
            return $"{subject} ma ten sam kolor co tło komórki ({ColorText.Hex(contrastCase.TextHex)}, kontrast 1:1) – treść jest niewidoczna. "
                + "Jeżeli ukryto ją celowo (np. instrukcję przeznaczoną dla czytników ekranu), kryterium 1.4.3 jej nie dotyczy; w przeciwnym razie zmień kolor czcionki.";
        }

        var facts = $"{subject} {ColorText.Hex(contrastCase.TextHex)} na tle {ColorText.Hex(contrastCase.BackgroundHex)} ma kontrast {WcagContrast.Format(contrastCase.Ratio)}, "
            + $"a wymagane jest co najmniej {ColorText.Required(contrastCase.Required)} ({Describe(contrastCase.SizePt, contrastCase.Bold)}).";
        return contrastCase.IsCertain
            ? facts + " Para kolorów jest pewna: oba kolory są zapisane w pliku wprost."
            : facts + " Para kolorów jest przybliżona – wymaga potwierdzenia: " + string.Join("; ", contrastCase.Reasons()) + ".";
    }

    internal static string Fix(ContrastCase contrastCase, out string? suggestion)
    {
        suggestion = null;
        if (contrastCase.IsInvisible)
        {
            return "Jeżeli tekst ma być widoczny, nadaj mu kolor kontrastujący z tłem: Narzędzia główne → Kolor czcionki (np. Automatyczny). "
                + "Jeżeli jest ukryty celowo, nic nie zmieniaj. Wynik sprawdź w Recenzja → Sprawdź ułatwienia dostępu.";
        }

        // Podpowiedź koloru tylko wtedy, gdy tło jest znane; przy tle przybliżonym albo zależnym od stylu tabeli wprowadzałaby w błąd.
        const Uncertainty UnknownBackground = Uncertainty.ApproximateBackground | Uncertainty.TableStyle | Uncertainty.ConditionalFormat;
        if ((contrastCase.Flags & UnknownBackground) == 0)
        {
            suggestion = ColorText.SuggestTextColor(contrastCase.TextHex, contrastCase.BackgroundHex, contrastCase.Required);
        }

        return suggestion is null
            ? BaseFix
            : BaseFix + $" Najbliższy kolor czcionki spełniający próg na tym tle: {ColorText.Hex(suggestion)} (kontrast {WcagContrast.Format(WcagContrast.Ratio(suggestion, contrastCase.BackgroundHex))}).";
    }

    internal static IReadOnlyList<XlsxEvidence> Evidence(ContrastCase contrastCase, long cells, string? suggestion)
    {
        var evidence = new List<XlsxEvidence>
        {
            new("Kontrast", WcagContrast.Format(contrastCase.Ratio)),
            new("Wymagany kontrast", ColorText.Required(contrastCase.Required)),
            new("Kolor tekstu", ColorText.Hex(contrastCase.TextHex)),
            new("Kolor tła", ColorText.Hex(contrastCase.BackgroundHex)),
            new("Zapis koloru tekstu", contrastCase.TextSource),
            new("Zapis koloru tła", contrastCase.BackgroundSource),
            new("Rozmiar czcionki", ColorText.Size(contrastCase.SizePt)),
            new("Pogrubienie", contrastCase.Bold ? "tak" : "nie"),
            new("Para kolorów", contrastCase.IsInvisible ? "tekst w kolorze tła" : contrastCase.IsCertain ? "pewna" : "przybliżona"),
            new("Liczba komórek", ColorText.Count(cells)),
        };
        if (contrastCase.Fragment is not null)
        {
            evidence.Add(new XlsxEvidence("Fragment tekstu", contrastCase.Fragment));
        }

        if (suggestion is not null)
        {
            evidence.Add(new XlsxEvidence("Proponowany kolor tekstu", ColorText.Hex(suggestion)));
        }

        return evidence;
    }

    internal static string Describe(double sizePt, bool bold) =>
        WcagContrast.IsLargeText(sizePt, bold)
            ? $"duży tekst: {ColorText.Size(sizePt)}{(bold ? ", pogrubiony" : "")}"
            : $"tekst {ColorText.Size(sizePt)}{(bold ? ", pogrubiony" : "")}";
}
