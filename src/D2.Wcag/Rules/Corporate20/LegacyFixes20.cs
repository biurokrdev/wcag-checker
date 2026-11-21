namespace D2.Wcag.Rules;

/// <summary>
/// Instrukcje naprawy krok po kroku (polski Word) dla kodów sprzed rejestru reguł, które zgłasza standard pism
/// firmowych 2.0 – także wspólnych z WCAG i standardem 1.9. Walidator dopisuje je zgłoszeniom tego standardu
/// (<see cref="Standards.IDocumentStandard.LegacyFixes"/>); komunikaty i wyniki innych standardów zostają bez zmian.
/// Nowe reguły (kody <c>F20-…</c>) mają instrukcję w deskryptorze.
/// </summary>
internal static class LegacyFixes20
{
    private const string Margins =
        "1. Układ → Marginesy → Marginesy niestandardowe. "
        + "2. W polach Górny, Dolny, Lewy i Prawy wpisz 1,27 cm. "
        + "3. Zastosuj do: Cały dokument → OK.";

    private const string Language =
        "1. Zaznacz cały tekst (Ctrl+A). "
        + "2. Recenzja → Język → Ustaw język sprawdzania. "
        + "3. Wybierz „Polski” (albo język treści dokumentu), kliknij Ustaw jako domyślny → OK.";

    public static IReadOnlyDictionary<string, string> For(string institution)
    {
        var author =
            "1. Plik → Informacje → Powiązane osoby → Autor. "
            + "2. Jeśli wpisany jest autor osobowy, usuń go (prawy przycisk myszy → Usuń osobę). "
            + $"3. Wpisz „{institution}”.";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [IssueCodes.PageFormatNotA4] =
                "1. Układ → Rozmiar → A4 (21 cm × 29,7 cm). "
                + "2. Gdy dokument ma kilka sekcji: Układ → Rozmiar → Więcej rozmiarów papieru → Zastosuj do: Cały dokument → OK.",
            [IssueCodes.MarginsMissing] = Margins,
            [IssueCodes.InvalidMargins] = Margins,
            [IssueCodes.JustificationForbidden] =
                "1. Zaznacz akapit. "
                + "2. Narzędzia główne → Wyrównaj do lewej (Ctrl+L). "
                + "3. Jeśli justowanie pochodzi ze stylu: Narzędzia główne → Style → prawy przycisk myszy na stylu → Modyfikuj → wyrównanie do lewej → OK.",
            [IssueCodes.FontFamilyMismatch] =
                "1. Zaznacz fragment. "
                + "2. Usuń formatowanie ręczne skrótem Ctrl+Spacja, żeby czcionkę nadał styl. "
                + "3. Jeśli zła czcionka jest w stylu: Narzędzia główne → Style → prawy przycisk myszy na stylu → Modyfikuj → czcionka SUN Me (dla stylu „Nagłówek 1”: SUN Display) → OK.",
            [IssueCodes.FontWeightInFamilyName] =
                "1. Zaznacz fragment. "
                + "2. Narzędzia główne → Czcionka → wybierz rodzinę „SUN Me” (albo „SUN Display”), a nie np. „SUN Me Bold”. "
                + "3. Pogrubienie włącz przyciskiem Pogrubienie (Ctrl+B) albo stylem.",
            [IssueCodes.LineSpacingMismatch] =
                "1. Zaznacz akapit. "
                + "2. Narzędzia główne → Akapit (strzałka w rogu grupy) → Interlinia: Wielokrotne, Co: 1,2 → OK. "
                + "3. Najlepiej ustaw to w stylu: prawy przycisk myszy na stylu → Modyfikuj → Format → Akapit.",
            [IssueCodes.BodyTextSizeMismatch] =
                "1. Zaznacz fragment. "
                + "2. Usuń formatowanie ręczne skrótem Ctrl+Spacja albo ustaw Narzędzia główne → Rozmiar czcionki: 10. "
                + "3. Rozmiar 12 albo 14 pt zostaw tylko tam, gdzie wymaga tego potrzeba biznesowa.",
            [IssueCodes.TextColorNotAllowed] =
                "1. Zaznacz fragment. "
                + "2. Narzędzia główne → Kolor czcionki → Więcej kolorów → Niestandardowy → wpisz kod koloru z palety (dla zwykłego tekstu 000000) → OK. "
                + "3. Albo usuń kolor nadany ręcznie skrótem Ctrl+Spacja, żeby kolor nadał styl.",
            [IssueCodes.TextColorUsageMismatch] =
                "1. Zaznacz fragment. "
                + "2. Narzędzia główne → Kolor czcionki → Więcej kolorów → Niestandardowy i wpisz kolor przewidziany dla tego rodzaju tekstu: zwykły tekst 000000, zapis zmienny 0000FF, znacznik wariantu AB0066, łącze 7724FF, stopka i dane rejestrowe 696969 (8 pt). "
                + "3. Pomarańczowy FF6200 zostaw dla nagłówka H1 i punktorów.",
            [IssueCodes.HeadingStyleMismatch] =
                "1. Kliknij nagłówek. "
                + "2. Narzędzia główne → Style → prawy przycisk myszy na stylu nagłówka → Modyfikuj. "
                + "3. Ustaw czcionkę, rozmiar, pogrubienie i kolor według tabeli typografii: H1 SUN Display 24 albo 32 pt, H2 16 pt, H3 14 pt, H4 12 pt, H5 10 pt SUN Me, wszystkie pogrubione → OK. "
                + "4. Formatowanie nadane ręcznie w nagłówku usuń skrótem Ctrl+Spacja.",
            [IssueCodes.HeadingSpacingMismatch] =
                "1. Narzędzia główne → Style → prawy przycisk myszy na stylu „Nagłówek 1” → Modyfikuj → Format → Akapit. "
                + "2. Odstępy: Przed 42,5 pt (w piśmie z polem adresowym ok. 210 pt), Po 34 pt → OK.",
            [IssueCodes.LinkStyleMismatch] =
                "1. Zaznacz łącze. "
                + "2. Usuń formatowanie ręczne skrótem Ctrl+Spacja albo wybierz Narzędzia główne → Style → „Hiperłącze”. "
                + "3. Styl „Hiperłącze” ma mieć kolor 7724FF i podkreślenie pojedyncze (prawy przycisk myszy na stylu → Modyfikuj).",
            [IssueCodes.TocRequired] =
                "1. Ustaw kursor w miejscu spisu treści. "
                + "2. Odwołania → Spis treści → Tabela automatyczna. "
                + "3. Po zmianach w dokumencie: prawy przycisk myszy na spisie → Aktualizuj pole → Aktualizacja całego spisu.",
            [IssueCodes.TocHeadingInToc] =
                "1. Kliknij nagłówek „Spis treści”. "
                + "2. Narzędzia główne → Style → „Nagłówek spisu treści” (nie „Nagłówek 1–5”). "
                + "3. Prawy przycisk myszy na spisie → Aktualizuj pole → Aktualizacja całego spisu.",
            [IssueCodes.TocHeadingStyleMismatch] =
                "1. Narzędzia główne → Style → prawy przycisk myszy na stylu „Nagłówek spisu treści” → Modyfikuj. "
                + "2. Ustaw czcionkę SUN Me, 10 pt, pogrubienie → OK.",
            [IssueCodes.ListBulletColorMismatch] =
                "1. Kliknij pozycję listy. "
                + "2. Narzędzia główne → Punktory (strzałka obok przycisku) → Zdefiniuj nowy punktor → Czcionka. "
                + "3. Kolor czcionki → Więcej kolorów → Niestandardowy → FF6200 albo 000000 → OK.",
            [IssueCodes.ListNumberNotBold] =
                "1. Kliknij pozycję listy. "
                + "2. Narzędzia główne → Numerowanie (strzałka obok przycisku) → Zdefiniuj nowy format numeracji → Czcionka. "
                + "3. Styl czcionki: Pogrubienie → OK.",
            [IssueCodes.HeadingLevelSkipped] =
                "1. Kliknij nagłówek. "
                + "2. Narzędzia główne → Style → wybierz poziom najwyżej o jeden niższy od poprzedniego nagłówka (po „Nagłówek 1” – „Nagłówek 2”). "
                + "3. Pierwszy nagłówek dokumentu ma mieć styl „Nagłówek 1”. "
                + "4. Kolejność poziomów sprawdź w Widok → Okienko nawigacji.",
            [IssueCodes.TableMissingHeaders] =
                "1. Zaznacz pierwszy wiersz tabeli. "
                + "2. Układ (karta narzędzi tabeli) → Powtórz wiersze nagłówka. "
                + "3. Albo: prawy przycisk myszy → Właściwości tabeli → Wiersz → Powtórz jako wiersz nagłówka na początku każdej strony → OK.",
            [IssueCodes.TableEmptyCell] =
                "1. Kliknij pustą komórkę. "
                + "2. Wpisz „Brak danych”, „0” albo inną wartość słowną zgodną ze znaczeniem komórki.",
            [IssueCodes.TableRowCanSplit] =
                "1. Zaznacz całą tabelę. "
                + "2. Prawy przycisk myszy → Właściwości tabeli → Wiersz. "
                + "3. Odznacz „Zezwalaj na dzielenie wierszy między strony” → OK.",
            [IssueCodes.TableBorderMismatch] =
                "1. Zaznacz tabelę albo wskazane komórki. "
                + "2. Projekt tabeli → Obramowania → Obramowanie i cieniowanie. "
                + "3. Ustawienie: Wszystkie, Styl: linia ciągła, Kolor: czarny, Szerokość: 1 pkt. "
                + "4. Zastosuj do: Tabela (albo Komórka) → OK.",
            [IssueCodes.TableHeaderNotBold] =
                "1. Zaznacz wiersz nagłówka tabeli. "
                + "2. Narzędzia główne → Pogrubienie (Ctrl+B). "
                + "3. Jeśli pogrubienie ma dawać styl tabeli: Projekt tabeli → zaznacz „Wiersz nagłówka” i sprawdź, że komórka nie ma wyłączonego pogrubienia.",
            [IssueCodes.TableAlignmentMismatch] =
                "1. Zaznacz wskazane komórki. "
                + "2. Układ (karta narzędzi tabeli) → grupa Wyrównanie → „Wyrównaj do środka z lewej” (tekst do lewej, w pionie na środku).",
            [IssueCodes.TableTitleMissing] =
                "1. Prawy przycisk myszy na tabeli → Właściwości tabeli → Tekst alternatywny. "
                + "2. W polu Tytuł wpisz krótką nazwę tabeli → OK.",
            [IssueCodes.TableDescriptionMissing] =
                "1. Prawy przycisk myszy na tabeli → Właściwości tabeli → Tekst alternatywny. "
                + "2. W polu Opis napisz, co tabela przedstawia (dłużej niż w tytule) → OK.",
            [IssueCodes.BrandImageMismatch] =
                "1. Prawy przycisk myszy na grafice → Wyświetl tekst alternatywny. "
                + $"2. Wpisz „{institution} logo” (logo poziome) albo „Symbol graficzny {institution}” (identyfikator graficzny). "
                + "3. Formatowanie obrazu → Rozmiar: logo 4,4 cm × 1 cm, symbol 2,2 cm × 2,2 cm.",
            [IssueCodes.FooterStyleMismatch] =
                "1. Kliknij dwukrotnie stopkę. "
                + "2. Zaznacz jej tekst, usuń formatowanie ręczne skrótem Ctrl+Spacja i wybierz Narzędzia główne → Style → „Stopka”. "
                + "3. Styl „Stopka” ma mieć czcionkę SUN Me, 8 pt, bez pogrubienia, kolor 696969 i interlinię 1,2 (prawy przycisk myszy na stylu → Modyfikuj).",
            [IssueCodes.FooterTitleMissing] =
                "1. Kliknij dwukrotnie stopkę. "
                + "2. Po numerze strony dopisz kropkę, spację i tytuł dokumentu, np. „Strona: 2 z 3. Regulamin promocji”. "
                + "3. Zamknij stopkę klawiszem Esc.",
            [IssueCodes.TitlePageFooterPresent] =
                "1. Kliknij dwukrotnie stopkę na stronie tytułowej. "
                + "2. Nagłówek i stopka → zaznacz „Inne na pierwszej stronie”. "
                + "3. Usuń treść stopki pierwszej strony; stopka kolejnych stron zostaje bez zmian.",
            [IssueCodes.TitleMissing] =
                "1. Plik → Informacje → Właściwości → pole Tytuł. "
                + $"2. Wpisz tytuł w postaci „[Tytuł dokumentu]. {institution}”.",
            [IssueCodes.TitleGeneric] =
                "1. Plik → Informacje → Właściwości → pole Tytuł. "
                + $"2. Zastąp nazwę domyślną albo nazwę pliku tytułem opisującym dokument, w postaci „[Tytuł dokumentu]. {institution}”.",
            [IssueCodes.TitleSuffixInvalid] =
                "1. Plik → Informacje → Właściwości → pole Tytuł. "
                + $"2. Po tytule dopisz kropkę, spację i „{institution}”, np. „Regulamin promocji. {institution}”.",
            [IssueCodes.AuthorMissing] = author,
            [IssueCodes.AuthorNotInstitution] = author,
            [IssueCodes.LanguageMissing] = Language,
            [IssueCodes.LanguageInvalid] = Language,
            [IssueCodes.VariantMarkerUnbalanced] =
                "1. Odszukaj wskazany znacznik wariantu. "
                + "2. Każdemu „[wariant N]” dopisz „[koniec wariantu N]” z tym samym numerem. "
                + "3. Wariant zagnieżdżony (np. 1.1) zamknij przed zamknięciem nadrzędnego (1); zakresy nie mogą się przecinać.",
            [IssueCodes.VariantMarkerColorMismatch] =
                "1. Zaznacz znacznik razem z nawiasami kwadratowymi. "
                + "2. Narzędzia główne → Style → „Znacznik wariantu”. "
                + "3. Jeśli szablon nie ma tego stylu: Narzędzia główne → Kolor czcionki → Więcej kolorów → Niestandardowy → AB0066 → OK.",
            [IssueCodes.VariableColorMismatch] =
                "1. Zaznacz zapis zmienny razem z nawiasami ostrymi. "
                + "2. Narzędzia główne → Style → „Zapis zmienny”. "
                + "3. Jeśli szablon nie ma tego stylu: Narzędzia główne → Kolor czcionki → Więcej kolorów → Niestandardowy → 0000FF → OK.",
            [IssueCodes.VariantInflectionShortcut] =
                "1. Zastąp skrót pełnymi, równoległymi wariantami, np. „Zostałam poinformowana [lub] Zostałem poinformowany”. "
                + "2. Przeczytaj całe zdanie w każdym wariancie i popraw składnię.",
            [IssueCodes.VariantSlashAlternative] =
                "1. Usuń ukośnik między alternatywami. "
                + "2. Wpisz w jego miejsce „[lub]” ze spacją z obu stron, np. „PLN [lub] EUR”. "
                + "3. Zaznacz „[lub]” i wybierz Narzędzia główne → Style → „Znacznik wariantu” (kolor AB0066).",
            [IssueCodes.SampleMarkStyleMismatch] =
                "1. Zaznacz napis WZÓR. "
                + "2. Narzędzia główne → Czcionka: SUN Me, Rozmiar czcionki: 28, Pogrubienie (Ctrl+B), Kolor czcionki: czarny. "
                + "3. Najlepiej zapisz to formatowanie jako styl akapitu napisu WZÓR.",
            [IssueCodes.SampleMarkNotOnEveryPage] =
                "1. Wytnij napis WZÓR z treści dokumentu (Ctrl+X). "
                + "2. Wstawianie → Nagłówek → Edytuj nagłówek. "
                + "3. Wklej napis w nagłówku strony (Ctrl+V) i zamknij nagłówek klawiszem Esc.",
        };
    }
}
