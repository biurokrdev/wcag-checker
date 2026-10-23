namespace D2.Wcag.Docx;

public interface IWcagDocxValidator
{
    WcagValidationResult Validate(byte[] docx, WcagValidationProfile? profile = null);

    WcagValidationResult Validate(Stream docx, WcagValidationProfile? profile = null);

    WcagValidationResult ValidateFile(string path, WcagValidationProfile? profile = null);

    Task<WcagValidationResult> ValidateAsync(Stream docx, WcagValidationProfile? profile = null, CancellationToken cancellationToken = default);
}
