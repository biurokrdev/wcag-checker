using D2.Wcag.Xlsx.Rules.Structure;

namespace D2.Wcag.Tests.Xlsx.Structure;

// Wzorce XL-SENSORY: zwroty PL i EN odwołujące się do koloru, położenia, kształtu; bez fałszywych trafień.
public class SensoryPatternsTests
{
    [Theory]
    [InlineData("Wartości zaznaczone na czerwono wymagają korekty.", "kolor")]
    [InlineData("Wypełnij żółte pola.", "kolor")]
    [InlineData("Pozycje wyróżnione kolorem zielonym są zatwierdzone.", "kolor")]
    [InlineData("Komórki w kolorze niebieskim wylicza formuła.", "kolor")]
    [InlineData("Kliknij czerwony przycisk, aby wysłać.", "kolor")]
    [InlineData("Podsumowanie znajduje się po prawej.", "położenie")]
    [InlineData("Objaśnienia z lewej strony arkusza.", "położenie")]
    [InlineData("Patrz tabela poniżej.", "położenie")]
    [InlineData("Poniżej znajduje się lista kontaktów.", "położenie")]
    [InlineData("Legenda u góry arkusza opisuje skróty.", "położenie")]
    [InlineData("Data aktualizacji w prawym górnym rogu.", "położenie")]
    [InlineData("Naciśnij okrągły przycisk.", "kształt lub rozmiar")]
    [InlineData("Values highlighted in red need attention.", "kolor")]
    [InlineData("Fill in the yellow cells.", "kolor")]
    [InlineData("The summary is on the right.", "położenie")]
    [InlineData("See the table below.", "położenie")]
    [InlineData("Click the round button to continue.", "kształt lub rozmiar")]
    [InlineData("Totals are at the top of the sheet.", "położenie")]
    public void Finds_SensoryPhrase(string text, string kind)
    {
        var found = SensoryPatterns.Find(text);

        Assert.Contains(found, f => f.Kind == kind);
    }

    [Theory]
    [InlineData("Prawo do odliczenia podatku VAT")]
    [InlineData("Prawa strona bilansu: pasywa")]
    [InlineData("Polski Czerwony Krzyż – darowizna")]
    [InlineData("Zielona energia – dotacje 2026")]
    [InlineData("Wynik poniżej 5% nie jest raportowany")]
    [InlineData("Przychody powyżej normy")]
    [InlineData("Szary Wilk sp. z o.o.")]
    [InlineData("Right to withdraw from the contract")]
    [InlineData("Red Cross donation")]
    [InlineData("Revenue below target")]
    [InlineData("Lewica i prawica – wyniki sondażu")]
    [InlineData("Kolumna Zygmunta, Warszawa")]
    public void Ignores_FalseFriends(string text)
    {
        Assert.Empty(SensoryPatterns.Find(text));
    }

    [Fact]
    public void Find_DeduplicatesAndKeepsOrder()
    {
        var found = SensoryPatterns.Find("Po prawej tabela; po prawej też wykres. Wypełnij żółte pola.");

        Assert.Equal(["Po prawej", "żółte pola"], found.Select(f => f.Phrase));
    }

    [Fact]
    public void Find_SkipsShortTexts()
    {
        Assert.Empty(SensoryPatterns.Find("po lewej"[..7]));
    }

    [Fact]
    public void Find_IsFastOnLongAdversarialText()
    {
        var text = string.Concat(Enumerable.Repeat("tabela aaa bbb ccc ", 20_000));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        SensoryPatterns.Find(text);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Dopasowanie trwało {stopwatch.Elapsed}.");
    }
}
