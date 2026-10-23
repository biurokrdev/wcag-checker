using DocumentFormat.OpenXml.Packaging;

namespace D2.Wcag.Docx;

// Wspólne otwieranie pakietu dla walidatora i naprawy: błąd formatu to wynik „nieczytelny”, nie wyjątek.
internal static class DocxOpener
{
    public static bool TryOpen(Stream stream, bool editable, out DocxDocument? document, out string? error)
    {
        document = null;
        error = null;
        WordprocessingDocument package;
        try
        {
            package = WordprocessingDocument.Open(stream, editable);
        }
        catch (Exception ex) when (IsFormatError(ex))
        {
            error = $"Nie udało się odczytać pliku jako DOCX: {ex.Message}";
            return false;
        }

        try
        {
            document = new DocxDocument(package);
            return true;
        }
        catch (Exception ex) when (IsFormatError(ex))
        {
            package.Dispose();
            error = $"Nie udało się odczytać pliku jako DOCX: {ex.Message}";
            return false;
        }
    }

    public static bool IsFormatError(Exception ex) =>
        ex is OpenXmlPackageException or FileFormatException or InvalidDataException or InvalidOperationException or IOException or ArgumentException;
}
