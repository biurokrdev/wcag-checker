using System.Text;
using D2.Wcag.SampleGenerator;

// Generuje pliki przykładowe do samples/ i ich manifesty (*.expected.json), a przed zapisem sprawdza je prawdziwym
// walidatorem: plik zgodny musi dać 0 błędów i 0 ostrzeżeń, a niezgodny – dokładnie zamierzony zestaw kodów reguł.
//   dotnet run --project samples/SampleGenerator              (zapis do samples/)
//   dotnet run --project samples/SampleGenerator -- <katalog>  (zapis do innego katalogu)
//   dotnet run --project samples/SampleGenerator -- --check    (porównanie z plikami w samples/ bez zapisu)
Console.OutputEncoding = Encoding.UTF8;

var check = args.Contains("--check");
var output = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Path.Combine("samples");
var verbose = args.Contains("--verbose");
var failed = false;

foreach (var sample in SampleCatalog.All)
{
    var bytes = sample.Build();
    var report = SampleVerifier.Verify(sample, bytes);
    Console.WriteLine($"{sample.FileName}: {report.Summary}");
    if (verbose || report.Problems.Count > 0)
    {
        foreach (var line in report.Details)
        {
            Console.WriteLine("    " + line);
        }
    }

    foreach (var problem in report.Problems)
    {
        Console.Error.WriteLine($"  BŁĄD: {problem}");
        failed = true;
    }

    var path = Path.Combine(output, sample.FileName);
    var manifestPath = Path.Combine(output, sample.ManifestName);
    if (check)
    {
        if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
        {
            Console.Error.WriteLine($"  BŁĄD: {path} różni się od wyniku generatora.");
            failed = true;
        }

        if (!File.Exists(manifestPath) || File.ReadAllText(manifestPath) != report.Manifest)
        {
            Console.Error.WriteLine($"  BŁĄD: {manifestPath} różni się od wyniku generatora.");
            failed = true;
        }

        continue;
    }

    if (report.Problems.Count == 0)
    {
        Directory.CreateDirectory(Path.GetFullPath(output));
        File.WriteAllBytes(path, bytes);
        File.WriteAllText(manifestPath, report.Manifest, new UTF8Encoding(false));
        Console.WriteLine($"  zapisano {Path.GetFullPath(path)} i {sample.ManifestName}");
    }
}

if (failed)
{
    Console.Error.WriteLine("Pliki z błędami nie zostały zapisane.");
}

return failed ? 1 : 0;
