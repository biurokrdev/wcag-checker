using System.IO.Compression;

namespace D2.Wcag.SampleGenerator;

/// <summary>
/// Przepakowanie pakietu OOXML zapisanego przez Open XML SDK tak, żeby ten sam generator dawał te same bajty:
/// stała data wpisów ZIP, stała kolejność ([Content_Types].xml, potem _rels/.rels, reszta alfabetycznie),
/// bez wpisów katalogów.
/// </summary>
public static class DeterministicZip
{
    private static readonly DateTimeOffset Timestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Repack(byte[] package)
    {
        var parts = new List<(string Name, byte[] Content)>();
        using (var archive = new ZipArchive(new MemoryStream(package, writable: false), ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/'))
                {
                    continue;
                }

                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                var content = buffer.ToArray();
                if (entry.FullName == "_rels/.rels")
                {
                    // System.IO.Packaging nadaje relacjom pakietu losowe identyfikatory („R” + 16 cyfr szesnastkowych);
                    // nic się do nich nie odwołuje, więc dostają kolejne numery.
                    var number = 0;
                    var xml = System.Text.RegularExpressions.Regex.Replace(
                        System.Text.Encoding.UTF8.GetString(content), "Id=\"R[0-9a-f]{16}\"", _ => $"Id=\"rIdPkg{++number}\"");
                    content = System.Text.Encoding.UTF8.GetBytes(xml);
                }

                parts.Add((entry.FullName, content));
            }
        }

        // Właściwości pakietu System.IO.Packaging zapisuje pod losową nazwą (…/core-properties/<guid>.psmdcp);
        // przenosimy je do docProps/core.xml, jak robi Word.
        var core = parts.FindIndex(p => p.Name.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal));
        if (core >= 0)
        {
            var oldName = "/" + parts[core].Name;
            parts[core] = ("docProps/core.xml", parts[core].Content);
            for (var i = 0; i < parts.Count; i++)
            {
                var (name, content) = parts[i];
                if (name == "_rels/.rels")
                {
                    parts[i] = (name, Replace(content, s => s.Replace(oldName, "/docProps/core.xml", StringComparison.Ordinal)));
                }
                else if (name == "[Content_Types].xml")
                {
                    parts[i] = (name, Replace(content, s =>
                    {
                        s = s.Replace(oldName, "/docProps/core.xml", StringComparison.Ordinal);
                        var type = "application/vnd.openxmlformats-package.core-properties+xml";
                        var defaultEntry = System.Text.RegularExpressions.Regex.Match(s, "<Default Extension=\"psmdcp\" ContentType=\"[^\"]+\" />");
                        if (defaultEntry.Success)
                        {
                            s = s.Remove(defaultEntry.Index, defaultEntry.Length);
                            if (!s.Contains("PartName=\"/docProps/core.xml\"", StringComparison.Ordinal))
                            {
                                s = s.Replace("</Types>", $"<Override PartName=\"/docProps/core.xml\" ContentType=\"{type}\" /></Types>", StringComparison.Ordinal);
                            }
                        }

                        return s;
                    }));
                }
            }
        }

        static byte[] Replace(byte[] content, Func<string, string> change) =>
            System.Text.Encoding.UTF8.GetBytes(change(System.Text.Encoding.UTF8.GetString(content)));

        static int Rank(string name) => name switch
        {
            "[Content_Types].xml" => 0,
            "_rels/.rels" => 1,
            _ => 2,
        };

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in parts.OrderBy(p => Rank(p.Name)).ThenBy(p => p.Name, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = Timestamp;
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        return output.ToArray();
    }
}
