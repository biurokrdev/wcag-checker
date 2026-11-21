using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace D2.Wcag.Tests;

// Snapshoty bez dodatkowych zależności. Plik wzorcowy leży w tests/snapshots (w repozytorium, nie w katalogu wyjściowym).
// Rozbieżność: test pada i zapisuje obok plik *.received.* (ignorowany przez git). Akceptacja zmian:
//   UPDATE_SNAPSHOTS=1 dotnet test, potem przegląd `git diff tests/snapshots`.
internal static class Snapshot
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static bool Updating => Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1";

    /// <summary>Porównuje <paramref name="actual"/> z plikiem <c>tests/snapshots/{relativePath}</c> (końce wierszy \n).</summary>
    public static void Match(string relativePath, string actual)
    {
        var path = Path.Combine(RepoPaths.Snapshots, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var received = ReceivedPath(path);
        actual = Normalize(actual);

        if (Updating)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path) || Normalize(File.ReadAllText(path, Utf8)) != actual)
            {
                File.WriteAllText(path, actual, Utf8);
            }

            File.Delete(received);
            return;
        }

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(received, actual, Utf8);
            Assert.Fail($"Brak snapshotu {relativePath}. Zapisano {Path.GetFileName(received)}; zaakceptuj przez UPDATE_SNAPSHOTS=1 dotnet test.");
        }

        var expected = Normalize(File.ReadAllText(path, Utf8));
        if (expected == actual)
        {
            File.Delete(received);
            return;
        }

        File.WriteAllText(received, actual, Utf8);
        Assert.Fail($"Snapshot {relativePath} różni się od wyniku. {FirstDifference(expected, actual)} Zapisano {Path.GetFileName(received)}; zaakceptuj przez UPDATE_SNAPSHOTS=1 dotnet test i przejrzyj git diff tests/snapshots.");
    }

    /// <summary>JSON do snapshotu: wcięcia, camelCase, enumy jako nazwy, bez wartości null, końce wierszy \n.</summary>
    public static string Json(object value) => Normalize(JsonSerializer.Serialize(value, JsonOptions));

    public static string Normalize(string text)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return text.EndsWith('\n') ? text : text + "\n";
    }

    private static string ReceivedPath(string path) =>
        Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + ".received" + Path.GetExtension(path));

    private static string FirstDifference(string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            var e = i < expectedLines.Length ? expectedLines[i] : "(koniec pliku)";
            var a = i < actualLines.Length ? actualLines[i] : "(koniec pliku)";
            if (e != a)
            {
                return $"Pierwsza różnica w wierszu {i + 1}: oczekiwano „{e.Trim()}”, jest „{a.Trim()}”.";
            }
        }

        return "";
    }
}

internal static class RepoPaths
{
    private static readonly Lazy<string> LazyRoot = new(() =>
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "D2.Wcag.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Nie znaleziono katalogu repozytorium (D2.Wcag.sln) nad katalogiem testów.");
    });

    public static string Root => LazyRoot.Value;

    public static string Snapshots => Path.Combine(Root, "tests", "snapshots");

    public static string Fixtures => Path.Combine(Root, "tests", "fixtures");
}

/// <summary>Dokumenty i profile objęte snapshotami: 3 pliki z samples/ i 5 fixtur × profile standardów.</summary>
internal static class SnapshotCases
{
    public static readonly string[] Samples = ["przyklad", "przyklad_v1", "szablon_pisma_2.0"];

    public static readonly string[] Fixtures = ["clean", "direct-formatting", "hierarchy-broken", "audit20-gaps", "edge"];

    public static readonly (string Name, string[] Standards)[] Profiles =
    [
        ("wcag", ["wcag/2.1"]),
        ("firmowy19", ["firmowy/1.9"]),
        ("firmowy20", ["firmowy/2.0"]),
        ("wcag+firmowy19", ["wcag/2.1", "firmowy/1.9"]),
        ("wcag+firmowy20", ["wcag/2.1", "firmowy/2.0"]),
        ("style", ["style/1.0"]),
        ("hierarchia", ["hierarchia/1.0"]),
    ];

    public static IEnumerable<string> Documents => Samples.Concat(Fixtures);

    /// <summary>Wiersze dla [MemberData]: (dokument, profil).</summary>
    public static IEnumerable<object[]> All() =>
        from document in Documents
        from profile in Profiles
        select new object[] { document, profile.Name };

    public static IEnumerable<object[]> AllDocuments() => Documents.Select(d => new object[] { d });

    public static string[] StandardsOf(string profile) => Profiles.Single(p => p.Name == profile).Standards;

    /// <summary>Bajty dokumentu z katalogu wyjściowego testów (samples/ albo fixtures/).</summary>
    public static byte[] Load(string document)
    {
        var folder = Samples.Contains(document) ? "samples" : "fixtures";
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, folder, document + ".docx"));
    }
}
