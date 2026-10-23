namespace D2.Wcag.Docx;

public interface IWcagDocxFixer
{
    /// <summary>Kody zgłoszeń, które naprawa usuwa automatycznie przy standardach z danego profilu (domyślnego, gdy <c>null</c>).</summary>
    IReadOnlySet<string> FixableCodes(WcagValidationProfile? profile = null);

    bool CanFix(string code, WcagValidationProfile? profile = null);

    /// <summary>Waliduje, naprawia to, co się da, i waliduje ponownie. Nieczytelny plik wraca bez zmian z wynikiem <c>Before</c> = <c>After</c>.</summary>
    WcagFixResult Fix(byte[] docx, WcagValidationProfile? profile = null, WcagFixOptions? options = null);

    WcagFixResult Fix(Stream docx, WcagValidationProfile? profile = null, WcagFixOptions? options = null);

    /// <summary>Czyta plik, zapisuje poprawioną wersję pod <paramref name="outputPath"/> (także gdy nic nie zmieniono) i zwraca wynik.</summary>
    WcagFixResult FixFile(string path, string outputPath, WcagValidationProfile? profile = null, WcagFixOptions? options = null);

    Task<WcagFixResult> FixAsync(Stream docx, WcagValidationProfile? profile = null, WcagFixOptions? options = null, CancellationToken cancellationToken = default);
}
