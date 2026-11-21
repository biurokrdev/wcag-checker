using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Color;

// Fixtura color.xlsx: próbki B2–B19 arkusza „Kontrast” (opis w komentarzu ColorXlsxFixture), zakres szarości,
// formatowanie warunkowe i wykresy. Wyniki audytu samymi regułami grupy „kolor i kontrast”.
public class ColorFixtureTests
{
    private const string Contrast = "XL-CONTRAST";
    private static XlsxAuditResult Result => ColorTestData.Fixture("color");

    private static XlsxFinding Sample(string cell) => Result.Single(Contrast, $"Kontrast!{cell}");

    [Theory]
    [InlineData("B2", "999999", "FFFFFF", "2,8:1", "4,5:1", "RGB")]
    [InlineData("B5", "C0C0C0", "FFFFFF", "1,8:1", "4,5:1", "indeks 22")]
    [InlineData("B8", "959595", "FFFFFF", "2,9:1", "3:1", "RGB")]
    [InlineData("B9", "949494", "FFFFFF", "3,0:1", "4,5:1", "RGB")]
    [InlineData("B12", "777777", "FFFFFF", "4,4:1", "4,5:1", "RGB")]
    [InlineData("B13", "FFFFFF", "FFC000", "1,6:1", "4,5:1", "RGB")]
    [InlineData("B18", "1F3864", "203764", "1,0:1", "4,5:1", "RGB")]
    public void ExplicitPairs_BelowThreshold_AreAutoErrors(string cell, string text, string background, string ratio, string required, string source)
    {
        var finding = Sample(cell);

        Assert.Equal((XlsxCheckType.Auto, XlsxSeverity.Error, (double?)null), (finding.CheckType, finding.Severity, finding.Confidence));
        Assert.Equal(["1.4.3"], finding.Wcag.Select(w => w.Sc));
        Assert.Equal(ratio, finding.Evidence("Kontrast"));
        Assert.Equal(required, finding.Evidence("Wymagany kontrast"));
        Assert.Equal("#" + text, finding.Evidence("Kolor tekstu"));
        Assert.Equal("#" + background, finding.Evidence("Kolor tła"));
        Assert.Equal(source, finding.Evidence("Zapis koloru tekstu"));
        Assert.Equal("pewna", finding.Evidence("Para kolorów"));
        Assert.Equal("1", finding.Evidence("Liczba komórek"));
        Assert.Contains("Para kolorów jest pewna", finding.Message);
        Assert.Contains($"#{text} na tle #{background} ma kontrast {ratio}", finding.Message);
        Assert.Contains("Narzędzia główne → Kolor czcionki", finding.FixInstruction);
    }

    [Theory]
    [InlineData("B4")]   // czarny motywu na jasnoszarym motywu – 18,7:1
    [InlineData("B6")]   // czarny na żółtym z palety indeksowanej
    [InlineData("B7")]   // 14 pt pogrubiony #949494 – 3,03:1 przy progu 3:1
    [InlineData("B10")]  // 18 pt #949494 – duży tekst
    [InlineData("B11")]  // #767676 – 4,54:1
    [InlineData("B14")]  // czarny na wzorze gray125 – tło przybliżone, kontrast wysoki
    [InlineData("B15")]  // kolor automatyczny bez wypełnienia
    [InlineData("B16")]  // pusta komórka ze stylem #999999 – pominięta
    public void PassingOrEmptySamples_AreNotReported(string cell)
    {
        Assert.DoesNotContain(Result.Findings, f => f.Location.Display == $"Kontrast!{cell}");
    }

    [Fact]
    public void Boundaries_AreComparedWithoutRounding()
    {
        // #767676 przechodzi (4,54:1), #777777 nie (4,478:1 pokazane jako 4,4:1, nie 4,5:1).
        Assert.Null(Result.At(Contrast, "Kontrast!B11"));
        Assert.Equal("4,4:1", Sample("B12").Evidence("Kontrast"));
        // 14 pt pogrubiony: #949494 przechodzi, #959595 (2,995:1) nie – pokazane jako 2,9:1, nie 3,0:1.
        Assert.Null(Result.At(Contrast, "Kontrast!B7"));
        Assert.Equal(("2,9:1", "3:1", "14 pt", "tak"),
            (Sample("B8").Evidence("Kontrast"), Sample("B8").Evidence("Wymagany kontrast"), Sample("B8").Evidence("Rozmiar czcionki"), Sample("B8").Evidence("Pogrubienie")));
        Assert.Contains("duży tekst: 14 pt, pogrubiony", Sample("B8").Message);
        // 14 pt bez pogrubienia nie jest dużym tekstem: próg 4,5:1.
        Assert.Equal(("4,5:1", "nie"), (Sample("B9").Evidence("Wymagany kontrast"), Sample("B9").Evidence("Pogrubienie")));
    }

    [Fact]
    public void ThemeColorWithTint_IsHeuristicWarning()
    {
        var finding = Sample("B3");

        Assert.Equal((XlsxCheckType.Heuristic, XlsxSeverity.Warning, (double?)0.9), (finding.CheckType, finding.Severity, finding.Confidence));
        Assert.Equal("#8EA9DB", finding.Evidence("Kolor tekstu"));
        Assert.Equal("motyw 4 (akcent 1) z odcieniem +40%", finding.Evidence("Zapis koloru tekstu"));
        Assert.Equal("przybliżona", finding.Evidence("Para kolorów"));
        Assert.Contains("Para kolorów jest przybliżona", finding.Message);
    }

    [Fact]
    public void RichTextFragment_IsEvaluatedSeparately()
    {
        var finding = Sample("B17");

        Assert.Equal(XlsxSeverity.Error, finding.Severity);
        Assert.Equal(("pilne", "#FF0000", "3,9:1"), (finding.Evidence("Fragment tekstu"), finding.Evidence("Kolor tekstu"), finding.Evidence("Kontrast")));
        Assert.StartsWith("Fragment tekstu „pilne” #FF0000", finding.Message);
        Assert.Equal("pilne", finding.Location.Snippet);
        // Fragment „Termin: ” w kolorze stylu komórki ma dobry kontrast – jedno zgłoszenie na komórkę.
        Assert.Single(Result.Findings, f => f.Location.Display == "Kontrast!B17");
    }

    [Fact]
    public void WhiteOnWhite_HasSeparateMessage_AndNeedsReview()
    {
        var finding = Sample("B19");

        Assert.Equal((XlsxCheckType.Heuristic, XlsxSeverity.NeedsReview, (double?)0.5), (finding.CheckType, finding.Severity, finding.Confidence));
        Assert.Contains("ma ten sam kolor co tło komórki (#FFFFFF, kontrast 1:1) – treść jest niewidoczna", finding.Message);
        Assert.Equal("tekst w kolorze tła", finding.Evidence("Para kolorów"));
        Assert.DoesNotContain("Proponowany", string.Join("|", finding.Evidence.Select(e => e.Name)));
    }

    [Fact]
    public void SameStyleCells_AreGroupedIntoOneRange_WithWeightInEvidence()
    {
        var finding = Result.Single(Contrast, "'Zakres szarości'!A2:C39");

        Assert.Equal(XlsxSeverity.Error, finding.Severity);
        Assert.Equal("114", finding.Evidence("Liczba komórek"));
        Assert.Equal(0, finding.MoreCount);
        Assert.Single(Result.Findings, f => f.Location.Sheet == "Zakres szarości");
        Assert.Equal("", finding.Location.Snippet);
    }

    [Fact]
    public void ContrastSamples_HaveExactlyExpectedLocations()
    {
        var locations = Result.Code(Contrast).Select(f => f.Location.Display).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(new[]
        {
            "'Zakres szarości'!A2:C39", "Kontrast!B12", "Kontrast!B13", "Kontrast!B17", "Kontrast!B18", "Kontrast!B19",
            "Kontrast!B2", "Kontrast!B3", "Kontrast!B5", "Kontrast!B8", "Kontrast!B9",
        }, locations);
    }

    [Fact]
    public void ConditionalFormats_ContrastAfterRule()
    {
        var red = Result.Single("XL-CONTRAST-CF", "'Formatowanie warunkowe'!B2:B6");
        Assert.Equal((XlsxCheckType.Heuristic, XlsxSeverity.Warning, (double?)0.8), (red.CheckType, red.Severity, red.Confidence));
        Assert.Equal(("#FF0000", "#FFFFFF", "3,9:1"), (red.Evidence("Kolor tekstu"), red.Evidence("Kolor tła"), red.Evidence("Kontrast")));
        Assert.Equal("styl komórki: brak wypełnienia (przyjęto białe tło)", red.Evidence("Zapis koloru tła"));
        Assert.Contains("tło pochodzi ze stylu komórek zakresu (najgorszy przypadek)", red.Message);
        Assert.Equal("5 z 5 komórek z treścią", red.Evidence("Liczba komórek"));
        Assert.Contains("Formatowanie warunkowe → Zarządzaj regułami", red.FixInstruction);

        var yellow = Result.Single("XL-CONTRAST-CF", "'Formatowanie warunkowe'!D2:D6");
        Assert.Equal(("#FFFFFF", "#FFFF00", "1,0:1"), (yellow.Evidence("Kolor tekstu"), yellow.Evidence("Kolor tła"), yellow.Evidence("Kontrast")));
        Assert.Contains("Oba kolory nadaje reguła", yellow.Message);

        // Reguła „> 1000” (zielony 006100 na C6EFCE, 6,4:1) nie jest zgłaszana; skala, ikony i paski nie mają stylu różnicowego.
        Assert.Equal(2, Result.Code("XL-CONTRAST-CF").Count());
    }

    [Fact]
    public void ConditionalFormats_ColorOnly()
    {
        var red = Result.Single("XL-COLOR-ONLY", "'Formatowanie warunkowe'!B2:B6");
        Assert.Equal((XlsxSeverity.NeedsReview, (double?)0.6), (red.Severity, red.Confidence));
        Assert.Equal("formatowanie warunkowe zmieniające tylko kolor", red.Evidence("Sposób użycia koloru"));
        Assert.Contains("zmienia wyłącznie kolor czcionki (#FF0000)", red.Message);

        var scale = Result.Single("XL-COLOR-ONLY", "'Formatowanie warunkowe'!C2:C6");
        Assert.Equal((XlsxSeverity.NeedsReview, (double?)0.5), (scale.Severity, scale.Confidence));
        Assert.Equal("#F8696B, #FFEB84, #63BE7B", scale.Evidence("Kolory skali"));

        Assert.NotNull(Result.At("XL-COLOR-ONLY", "'Formatowanie warunkowe'!D2:D6"));
        // Pogrubienie w regule „> 1000”, strzałki (3Arrows) i paski danych to wyróżniki inne niż kolor.
        Assert.Equal(3, Result.Code("XL-COLOR-ONLY").Count());
        Assert.All(Result.Code("XL-COLOR-ONLY"), f => Assert.Equal(["1.4.1"], f.Wcag.Select(w => w.Sc)));
    }

    [Fact]
    public void Charts_ColorOnlyAndContrast()
    {
        const string Bars = "Wykres „Wykres 1” na arkuszu „Wykresy kolorów”";
        const string Lines = "Wykres „Wykres 2” na arkuszu „Wykresy kolorów”";
        const string Distinct = "Wykres „Wykres 3” na arkuszu „Wykresy kolorów”";

        var bars = Result.Single("XL-CHART-COLOR", Bars);
        Assert.Equal((XlsxSeverity.Warning, (double?)0.8), (bars.Severity, bars.Confidence));
        Assert.Contains("„Produkt A” (#FF0000) i „Produkt B” (#00B050) – kontrast 1,3:1", bars.Message);
        Assert.Contains("Wypełnienie deseniem", bars.FixInstruction);
        Assert.Contains("Etykiety danych", bars.FixInstruction);
        Assert.NotNull(Result.At("XL-CHART-COLOR", Lines));
        Assert.Null(Result.At("XL-CHART-COLOR", Distinct));

        var barContrast = Result.Single("XL-CHART-CONTRAST", Bars);
        Assert.Equal((XlsxSeverity.Warning, "1,0:1", "#FFFFFF"), (barContrast.Severity, barContrast.Evidence("Najniższy kontrast"), barContrast.Evidence("Tło obszaru kreślenia")));
        Assert.Contains("„Produkt C” #FFFF00 względem tła #FFFFFF: 1,0:1", barContrast.Message);
        Assert.Contains("względem sąsiedniej serii", barContrast.Message);
        Assert.Equal(["1.4.11"], barContrast.Wcag.Select(w => w.Sc));

        var lineContrast = Result.Single("XL-CHART-CONTRAST", Lines);
        Assert.Equal("1", lineContrast.Evidence("Liczba elementów poniżej progu"));
        Assert.DoesNotContain("sąsiedniej", lineContrast.Message);
        Assert.Null(Result.At("XL-CHART-CONTRAST", Distinct));
    }

    [Fact]
    public void ManualChecks_AndCriteria()
    {
        Assert.Equal(["manual-1.4.1:color", "manual-1.4.3:color", "manual-1.4.3:exceptions", "manual-1.4.11:color"], Result.ManualIds());
        var background = Result.ManualChecks.Single(c => c.Id == "manual-1.4.3:color");
        Assert.Contains("wypełnieniem deseniem albo gradientem", background.Hint);
        Assert.Contains("na wykresach", background.Hint);

        var criteria = Result.Criteria.ToDictionary(c => c.Sc);
        Assert.Equal(XlsxCriterionStatus.Failed, criteria["1.4.3"].Status);
        Assert.Equal(XlsxCriterionStatus.ManualReview, criteria["1.4.1"].Status);
        Assert.Equal(XlsxCriterionStatus.ManualReview, criteria["1.4.11"].Status);
    }

    [Fact]
    public void Audit_IsDeterministic()
    {
        static string Dump(XlsxAuditResult r) => string.Join("\n", r.Findings.Select(f =>
            $"{f.Code}|{f.Severity}|{f.Confidence}|{f.Location.Key}|{f.Message}|{f.FixInstruction}|{string.Join(";", f.Evidence)}|{f.MoreCount}"))
            + "\n" + string.Join("\n", r.ManualChecks.Select(c => $"{c.Id}|{c.Question}|{c.Hint}"));

        var bytes = XlsxSnapshotCases.Load("color");
        var first = Dump(ColorTestData.Audit(bytes));
        var second = Dump(ColorTestData.Audit(bytes));

        Assert.Equal(first, second);
        Assert.Equal(first, Dump(Result));
    }
}

// Pozostałe fixtury: co grupa zgłasza poza color.xlsx.
public class ColorOtherFixturesTests
{
    [Fact]
    public void Clean_HasNoFindings_OnlyChecklist()
    {
        var result = ColorTestData.Fixture("clean");

        Assert.Empty(result.Findings);
        // Styl tabeli i wykres z tekstem – kontrast poza komórkami do oceny ręcznej; wykres – 1.4.11.
        Assert.Equal(["manual-1.4.3:color", "manual-1.4.11:color"], result.ManualIds());
    }

    [Fact]
    public void NonText_ChartsWithAutomaticColors_NeedReview()
    {
        var result = ColorTestData.Fixture("nontext");

        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, f =>
        {
            Assert.Equal(("XL-CHART-COLOR", XlsxSeverity.NeedsReview, (double?)0.5), (f.Code, f.Severity, f.Confidence));
            Assert.Contains("kolor automatyczny", f.Message);
        });
        // Serie w kolorach automatycznych nie są oceniane w XL-CHART-CONTRAST (kolor nieznany) – zostaje pytanie checklisty.
        Assert.Empty(result.Code("XL-CHART-CONTRAST"));
        Assert.Contains("manual-1.4.11:color", result.ManualIds());
    }

    [Theory]
    [InlineData("structure")]
    [InlineData("forms")]
    [InlineData("edge")]
    [InlineData("empty")]
    [InlineData("macro")]
    public void OtherFixtures_HaveNoColorFindings(string fixture)
    {
        Assert.Empty(ColorTestData.Fixture(fixture).Findings);
    }

    [Fact]
    public void Edge_HiddenByFormat_AndRichFragments_AreNotReported()
    {
        // A2 „;;;” (treść ukryta), A3 fragment FF0000 pogrubiony 14 pt (duży tekst, 4,0:1 ≥ 3:1), B3 fragment akcent 1, A5 006100.
        var result = ColorTestData.Fixture("edge");

        Assert.DoesNotContain(result.Findings, f => f.Location.Sheet == "Zażółć gęślą jaźń");
        Assert.Contains("manual-1.4.1:color", result.ManualIds());
    }

    [Fact]
    public void Empty_HasNoChecklist()
    {
        Assert.Empty(ColorTestData.Fixture("empty").ManualIds());
    }
}
