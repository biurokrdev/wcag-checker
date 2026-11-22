namespace D2.Wcag.SampleGenerator;

/// <summary>
/// Dane przykładowe: fikcyjna firma, osoby i adresy. Numery NIP i REGON mają poprawne sumy kontrolne (to numery
/// powszechnie używane jako przykłady), KRS ma poprawny format 10 cyfr i leży poza zakresem nadanych numerów.
/// Nazwa „Example Company S. A.” jest wymagana przez standard pism firmowych 2.0 (autor i dopisek w tytule).
/// </summary>
public static class FictionalData
{
    public const string Company = Standards.CorporateStandard20.DefaultInstitution;

    public const string CompanyAddress = "ul. Przykładowa 12, 00-950 Warszawa";

    public const string Phone = "+48 22 000 00 00";

    public const string Nip = "123-456-32-18";

    public const string Regon = "123456785";

    public const string Krs = "0098765432";

    public const string ShareCapital = "1 000 000 zł";

    public const string Recipient = "Pan Jan Przykładowy";

    public const string RecipientCompany = "Przykładowa Hurtownia Papieru sp. z o.o.";

    public const string RecipientStreet = "ul. Testowa 5";

    public const string RecipientCity = "31-000 Kraków";

    public const string Signatory = "Anna Wzorcowa";

    public const string SignatoryRole = "Kierownik Działu Obsługi Klienta";

    public const string Department = "Dział Administracji";

    public const string Marker = "Dane przykładowe";

    /// <summary>Blok danych rejestrowych (rozdz. 10 standardu 2.0).</summary>
    public static string RegistrationData =>
        $"{Company}, {CompanyAddress}. KRS {Krs}, NIP {Nip}, REGON {Regon}, kapitał zakładowy {ShareCapital} (wpłacony w całości). {Marker} – podmiot fikcyjny.";

    public static bool IsValidNip(string nip)
    {
        var digits = nip.Where(char.IsAsciiDigit).Select(c => c - '0').ToArray();
        int[] weights = [6, 5, 7, 2, 3, 4, 5, 6, 7];
        return digits.Length == 10 && weights.Select((w, i) => w * digits[i]).Sum() % 11 == digits[9];
    }

    public static bool IsValidRegon9(string regon)
    {
        var digits = regon.Where(char.IsAsciiDigit).Select(c => c - '0').ToArray();
        int[] weights = [8, 9, 2, 3, 4, 5, 6, 7];
        return digits.Length == 9 && weights.Select((w, i) => w * digits[i]).Sum() % 11 % 10 == digits[8];
    }

    public static bool IsValidKrsFormat(string krs) => krs.Length == 10 && krs.All(char.IsAsciiDigit);
}
