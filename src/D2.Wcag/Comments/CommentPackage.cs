using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace D2.Wcag.Docx;

// Składa nowy pakiet z oryginalnego: wszystkie wpisy ZIP przechodzą bajt w bajt, podmieniane są tylko część główna
// i część komentarzy, a gdy komentarzy nie było – dochodzą część komentarzy, jej relacja w relacjach części głównej
// i wpis w [Content_Types].xml. Pakiet nie przechodzi przez System.IO.Packaging, więc style, nagłówki, stopki i media
// zostają nietknięte, a wynik jest deterministyczny (stała kolejność wpisów, data nowych wpisów = data komentarzy).
internal static class CommentPackage
{
    private const string CommentsRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments";
    private const string CommentsContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml";
    private const string ContentTypesEntry = "[Content_Types].xml";

    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly DateTimeOffset ZipEpoch = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <param name="original">Oryginalny pakiet.</param>
    /// <param name="mainPartUri">Adres części głównej, np. <c>/word/document.xml</c>.</param>
    /// <param name="documentXml">Nowa zawartość części głównej.</param>
    /// <param name="commentsPartUri">Adres istniejącej części komentarzy albo <c>null</c>, gdy trzeba ją utworzyć.</param>
    /// <param name="commentsXml">Nowa zawartość części komentarzy.</param>
    /// <param name="date">Data wpisów ZIP, które powstają albo się zmieniają.</param>
    public static byte[] Write(byte[] original, Uri mainPartUri, byte[] documentXml, Uri? commentsPartUri, byte[] commentsXml, DateTimeOffset date)
    {
        var entries = new List<Entry>();
        using (var source = new ZipArchive(new MemoryStream(original, writable: false), ZipArchiveMode.Read))
        {
            foreach (var entry in source.Entries)
            {
                // Wpisy katalogów nie należą do pakietu OPC (Word potrafi odrzucić plik, który je ma).
                if (entry.FullName.EndsWith('/'))
                {
                    continue;
                }

                using var data = new MemoryStream();
                using (var input = entry.Open())
                {
                    input.CopyTo(data);
                }

                entries.Add(new Entry(entry.FullName, data.ToArray(), entry.LastWriteTime));
            }
        }

        var changed = date < ZipEpoch ? ZipEpoch : date;
        var mainName = EntryName(mainPartUri);
        Replace(entries, mainName, documentXml, changed);

        if (commentsPartUri is not null)
        {
            Replace(entries, EntryName(commentsPartUri), commentsXml, changed);
        }
        else
        {
            var slash = mainName.LastIndexOf('/');
            var folder = mainName[..(slash + 1)];
            var mainFile = mainName[(slash + 1)..];

            var commentsFile = "comments.xml";
            for (var n = 2; Find(entries, folder + commentsFile) >= 0; n++)
            {
                commentsFile = $"comments{n}.xml";
            }

            entries.Add(new Entry(folder + commentsFile, commentsXml, changed));
            AddRelationship(entries, $"{folder}_rels/{mainFile}.rels", commentsFile, changed);
            AddContentType(entries, "/" + folder + commentsFile, changed);
        }

        using var result = new MemoryStream();
        using (var target = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var created = target.CreateEntry(entry.Name, CompressionLevel.Optimal);
                created.LastWriteTime = entry.Time < ZipEpoch ? ZipEpoch : entry.Time;
                using var output = created.Open();
                output.Write(entry.Data);
            }
        }

        return result.ToArray();
    }

    private static string EntryName(Uri partUri) => Uri.UnescapeDataString(partUri.OriginalString).TrimStart('/');

    private static int Find(List<Entry> entries, string name) =>
        entries.FindIndex(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    private static void Replace(List<Entry> entries, string name, byte[] data, DateTimeOffset time)
    {
        var index = Find(entries, name);
        if (index < 0)
        {
            throw new CommentExportException($"Pakiet DOCX nie zawiera części „{name}” – plik jest uszkodzony albo ma nietypową budowę.");
        }

        entries[index] = new Entry(entries[index].Name, data, time);
    }

    private static void AddRelationship(List<Entry> entries, string relsName, string target, DateTimeOffset time)
    {
        var index = Find(entries, relsName);
        var document = index >= 0
            ? Load(entries[index].Data)
            : new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(Relationships + "Relationships"));
        var root = document.Root!;

        var used = root.Elements(Relationships + "Relationship").Select(r => (string?)r.Attribute("Id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var next = used
            .Where(id => id.StartsWith("rId", StringComparison.Ordinal) && int.TryParse(id.AsSpan(3), out _))
            .Select(id => int.Parse(id.AsSpan(3)))
            .DefaultIfEmpty(0)
            .Max() + 1;
        while (used.Contains($"rId{next}"))
        {
            next++;
        }

        root.Add(new XElement(
            Relationships + "Relationship",
            new XAttribute("Id", $"rId{next}"),
            new XAttribute("Type", CommentsRelationshipType),
            new XAttribute("Target", target)));

        var entry = new Entry(index >= 0 ? entries[index].Name : relsName, Save(document), time);
        if (index >= 0)
        {
            entries[index] = entry;
        }
        else
        {
            entries.Add(entry);
        }
    }

    private static void AddContentType(List<Entry> entries, string partName, DateTimeOffset time)
    {
        var index = Find(entries, ContentTypesEntry);
        if (index < 0)
        {
            throw new CommentExportException("Pakiet DOCX nie zawiera spisu typów zawartości ([Content_Types].xml) – plik jest uszkodzony.");
        }

        var document = Load(entries[index].Data);
        var root = document.Root!;
        var existing = root.Elements(ContentTypes + "Override")
            .FirstOrDefault(o => string.Equals((string?)o.Attribute("PartName"), partName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.SetAttributeValue("ContentType", CommentsContentType);
        }
        else
        {
            root.Add(new XElement(ContentTypes + "Override", new XAttribute("PartName", partName), new XAttribute("ContentType", CommentsContentType)));
        }

        entries[index] = new Entry(entries[index].Name, Save(document), time);
    }

    private static XDocument Load(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static byte[] Save(XDocument document)
    {
        document.Declaration = new XDeclaration("1.0", "UTF-8", "yes");
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), Indent = false }))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    private sealed record Entry(string Name, byte[] Data, DateTimeOffset Time);
}
