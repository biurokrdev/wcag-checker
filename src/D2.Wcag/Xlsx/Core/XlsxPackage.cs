using System.Diagnostics;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using D2.Wcag.Packages;

namespace D2.Wcag.Xlsx;

/// <summary>Odmowa odczytu pakietu z powodem i polskim komunikatem; audytor zamienia ją na wynik <c>IsReadable = false</c>.</summary>
internal sealed class XlsxReadException : Exception
{
    public XlsxReadException(XlsxFailure failure, string message, Exception? inner = null)
        : base(message, inner)
    {
        Failure = failure;
    }

    public XlsxFailure Failure { get; }
}

/// <summary>Budżet odczytu: faktycznie rozpakowane bajty, liczba komórek, czas i anulowanie.</summary>
internal sealed class XlsxReadBudget
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _bytes;
    private long _cells;

    public XlsxReadBudget(XlsxLimits limits, CancellationToken cancellationToken)
    {
        Limits = limits;
        CancellationToken = cancellationToken;
    }

    public XlsxLimits Limits { get; }

    public CancellationToken CancellationToken { get; }

    public long BytesRead => _bytes;

    public long Cells => _cells;

    /// <summary>Sprawdza anulowanie i limit czasu; wołane przy każdym odczycie ze strumienia części i w pętlach.</summary>
    public void Check()
    {
        CancellationToken.ThrowIfCancellationRequested();
        if (_stopwatch.Elapsed > Limits.Timeout)
        {
            throw new XlsxReadException(XlsxFailure.Timeout,
                $"Przekroczono limit czasu analizy skoroszytu ({FormatSeconds(Limits.Timeout)}). Plik jest zbyt duży albo zbyt złożony.");
        }
    }

    public void AddBytes(int count)
    {
        _bytes += count;
        if (_bytes > Limits.MaxUncompressedBytes)
        {
            throw new XlsxReadException(XlsxFailure.LimitExceeded,
                $"Skoroszyt po rozpakowaniu przekracza dozwolony rozmiar ({Megabytes(Limits.MaxUncompressedBytes)} MB).");
        }
    }

    public void AddCell()
    {
        if (++_cells > Limits.MaxCells)
        {
            throw new XlsxReadException(XlsxFailure.LimitExceeded,
                $"Skoroszyt ma więcej komórek, niż wynosi limit analizy ({Limits.MaxCells}).");
        }
    }

    internal static long Megabytes(long bytes) => Math.Max(1, bytes / (1024 * 1024));

    private static string FormatSeconds(TimeSpan time) =>
        time.TotalSeconds >= 1 ? $"{time.TotalSeconds:0} s" : $"{time.TotalMilliseconds:0} ms";
}

internal sealed record XlsxRelationship(string Id, string Type, string Target, bool External);

/// <summary>
/// Pakiet OPC otwarty z pamięci: wstępna kontrola katalogu ZIP, a potem odczyt części przez strumień z licznikiem
/// faktycznie rozpakowanych bajtów. Zadeklarowanym rozmiarom wpisów nie ufamy – służą tylko do wczesnej odmowy.
/// </summary>
internal sealed class XlsxPackage : IDisposable
{
    /// <summary>Limit rozmiaru części wczytywanych w całości do drzewa XML (wszystko poza arkuszami i tekstami wspólnymi).</summary>
    public const long MaxDomPartBytes = 32L * 1024 * 1024;

    private const long RatioCheckFrom = 1024 * 1024;

    private readonly ZipArchive _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly XlsxReadBudget _budget;

    private XlsxPackage(ZipArchive archive, XlsxReadBudget budget)
    {
        _archive = archive;
        _budget = budget;
    }

    public IEnumerable<string> Names => _entries.Keys;

    public static XlsxPackage Open(byte[] content, XlsxReadBudget budget)
    {
        var limits = budget.Limits;
        if (DeclaredEntryCount(content) is { } declared && declared > limits.MaxEntries)
        {
            throw TooManyEntries(limits);
        }

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            throw new XlsxReadException(XlsxFailure.Corrupted, "Plik jest uszkodzony: nie udało się odczytać archiwum ZIP.", exception);
        }

        var package = new XlsxPackage(archive, budget);
        try
        {
            package.Scan();
            return package;
        }
        catch
        {
            package.Dispose();
            throw;
        }
    }

    public bool Has(string partName) => _entries.ContainsKey(partName);

    public long CompressedLength(string partName) => _entries.TryGetValue(partName, out var entry) ? entry.CompressedLength : 0;

    /// <summary>Strumień części z limitami liczonymi na faktycznie przeczytanych bajtach; <c>null</c>, gdy części nie ma.</summary>
    public Stream? Open(string partName, long maxBytes = long.MaxValue)
    {
        if (!_entries.TryGetValue(partName, out var entry))
        {
            return null;
        }

        return new LimitedStream(entry.Open(), partName, Math.Min(maxBytes, _budget.Limits.MaxPartBytes), entry.CompressedLength, _budget);
    }

    /// <summary>Część XML jako drzewo (małe części: style, motyw, rysunki, wykresy…); <c>null</c>, gdy części nie ma.</summary>
    public XElement? LoadXml(string? partName)
    {
        if (partName is null)
        {
            return null;
        }

        using var stream = Open(partName, MaxDomPartBytes);
        if (stream is null)
        {
            return null;
        }

        using var reader = XmlReader.Create(stream, SafeXml.Settings());
        return XDocument.Load(reader).Root;
    }

    /// <summary>Relacje części (<c>""</c> = relacje pakietu). Cele wewnętrzne rozwinięte do nazw części; zewnętrzne tylko jako tekst.</summary>
    public IReadOnlyList<XlsxRelationship> Relationships(string partName)
    {
        var slash = partName.LastIndexOf('/');
        var folder = slash < 0 ? "" : partName[..(slash + 1)];
        var file = slash < 0 ? partName : partName[(slash + 1)..];
        var root = LoadXml($"{folder}_rels/{file}.rels");
        if (root is null)
        {
            return [];
        }

        var result = new List<XlsxRelationship>();
        foreach (var element in root.Els("Relationship"))
        {
            var id = element.Attr("Id");
            var target = element.Attr("Target");
            if (id is null || target is null)
            {
                continue;
            }

            var type = element.Attr("Type") ?? "";
            var typeName = type[(type.LastIndexOf('/') + 1)..];
            var external = string.Equals(element.Attr("TargetMode"), "External", StringComparison.OrdinalIgnoreCase);
            if (external)
            {
                result.Add(new XlsxRelationship(id, typeName, target, true));
            }
            else if (ResolveTarget(folder, target) is { } resolved)
            {
                result.Add(new XlsxRelationship(id, typeName, resolved, false));
            }
        }

        return result;
    }

    public void Dispose() => _archive.Dispose();

    /// <summary>Nazwa części docelowej względem folderu części źródłowej; <c>null</c>, gdy cel wychodzi poza pakiet.</summary>
    internal static string? ResolveTarget(string folder, string target)
    {
        var fragment = target.IndexOf('#');
        if (fragment >= 0)
        {
            target = target[..fragment];
        }

        var path = target.StartsWith('/') ? target : folder + target;
        var segments = new List<string>();
        foreach (var segment in path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }

    /// <summary>Liczba wpisów z rekordu końca katalogu centralnego – żeby odmówić, zanim biblioteka wczyta cały katalog.</summary>
    private static int? DeclaredEntryCount(byte[] content)
    {
        var start = Math.Max(0, content.Length - (22 + 65535));
        for (var i = content.Length - 22; i >= start; i--)
        {
            if (content[i] == 0x50 && content[i + 1] == 0x4B && content[i + 2] == 0x05 && content[i + 3] == 0x06)
            {
                var count = content[i + 10] | (content[i + 11] << 8);
                return count == 0xFFFF ? null : count;
            }
        }

        return null;
    }

    private static XlsxReadException TooManyEntries(XlsxLimits limits) =>
        new(XlsxFailure.LimitExceeded, $"Skoroszyt zawiera zbyt wiele części (limit: {limits.MaxEntries}).");

    private void Scan()
    {
        var limits = _budget.Limits;
        var entries = _archive.Entries;
        if (entries.Count > limits.MaxEntries)
        {
            throw TooManyEntries(limits);
        }

        long total = 0;
        foreach (var entry in entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(segment => segment is ".." or "."))
            {
                throw new XlsxReadException(XlsxFailure.Corrupted,
                    "Plik jest uszkodzony albo spreparowany: archiwum zawiera niedozwolone ścieżki części.");
            }

            if (name.EndsWith('/'))
            {
                continue;
            }

            if (!_entries.TryAdd(name, entry))
            {
                throw new XlsxReadException(XlsxFailure.Corrupted,
                    "Plik jest uszkodzony albo spreparowany: archiwum zawiera powtórzone nazwy części.");
            }

            if (entry.Length > limits.MaxPartBytes)
            {
                throw new XlsxReadException(XlsxFailure.LimitExceeded,
                    $"Część skoroszytu po rozpakowaniu przekracza dozwolony rozmiar ({XlsxReadBudget.Megabytes(limits.MaxPartBytes)} MB).");
            }

            if (entry.Length > RatioCheckFrom && entry.Length > Math.Max(entry.CompressedLength, 1) * limits.MaxCompressionRatio)
            {
                throw SuspiciousRatio();
            }

            total += entry.Length;
            if (total > limits.MaxUncompressedBytes)
            {
                throw new XlsxReadException(XlsxFailure.LimitExceeded,
                    $"Skoroszyt po rozpakowaniu przekracza dozwolony rozmiar ({XlsxReadBudget.Megabytes(limits.MaxUncompressedBytes)} MB).");
            }
        }
    }

    private static XlsxReadException SuspiciousRatio() =>
        new(XlsxFailure.LimitExceeded, "Skoroszyt ma podejrzanie wysoki współczynnik kompresji (możliwa „bomba ZIP”) i nie został przeanalizowany.");

    /// <summary>Strumień tylko do odczytu, który liczy faktycznie rozpakowane bajty i przerywa po przekroczeniu limitów.</summary>
    internal sealed class LimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly string _partName;
        private readonly long _maxBytes;
        private readonly long _compressedLength;
        private readonly XlsxReadBudget _budget;
        private long _read;

        public LimitedStream(Stream inner, string partName, long maxBytes, long compressedLength, XlsxReadBudget budget)
        {
            _inner = inner;
            _partName = partName;
            _maxBytes = maxBytes;
            _compressedLength = compressedLength;
            _budget = budget;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            _budget.Check();
            var read = _inner.Read(buffer);
            if (read <= 0)
            {
                return read;
            }

            _read += read;
            if (_read > _maxBytes)
            {
                throw new XlsxReadException(XlsxFailure.LimitExceeded,
                    $"Część skoroszytu „{_partName}” po rozpakowaniu przekracza dozwolony rozmiar ({XlsxReadBudget.Megabytes(_maxBytes)} MB).");
            }

            if (_read > RatioCheckFrom && _read > Math.Max(_compressedLength, 1) * _budget.Limits.MaxCompressionRatio)
            {
                throw SuspiciousRatio();
            }

            _budget.AddBytes(read);
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>Pomocnicze dopasowanie XML po nazwach lokalnych – wspólne dla Transitional i Strict OOXML.</summary>
internal static class XlsxXml
{
    public static XElement? El(this XElement? element, string localName)
    {
        if (element is null)
        {
            return null;
        }

        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName == localName)
            {
                return child;
            }
        }

        return null;
    }

    public static IEnumerable<XElement> Els(this XElement? element, string localName) =>
        element is null ? [] : element.Elements().Where(e => e.Name.LocalName == localName);

    public static IEnumerable<XElement> Desc(this XElement? element, string localName) =>
        element is null ? [] : element.Descendants().Where(e => e.Name.LocalName == localName);

    /// <summary>Atrybut po nazwie lokalnej: najpierw bez przestrzeni nazw, potem w dowolnej (np. <c>r:id</c>).</summary>
    public static string? Attr(this XElement? element, string localName)
    {
        if (element is null)
        {
            return null;
        }

        string? qualified = null;
        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || attribute.Name.LocalName != localName)
            {
                continue;
            }

            if (attribute.Name.NamespaceName.Length == 0)
            {
                return attribute.Value;
            }

            qualified ??= attribute.Value;
        }

        return qualified;
    }

    /// <summary>Atrybut z przestrzeni nazw relacji (<c>r:id</c>, <c>r:embed</c>).</summary>
    public static string? RelAttr(this XElement? element, string localName)
    {
        if (element is null)
        {
            return null;
        }

        foreach (var attribute in element.Attributes())
        {
            if (!attribute.IsNamespaceDeclaration && attribute.Name.LocalName == localName && attribute.Name.NamespaceName.Length > 0)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    /// <summary>Dzieci elementu z rozwinięciem <c>mc:AlternateContent</c> (pierwsza gałąź <c>Choice</c>, w razie braku <c>Fallback</c>).</summary>
    public static IEnumerable<XElement> Flat(this XElement? element)
    {
        if (element is null)
        {
            yield break;
        }

        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName != "AlternateContent")
            {
                yield return child;
                continue;
            }

            var branch = child.El("Choice") ?? child.El("Fallback");
            foreach (var nested in branch.Flat())
            {
                yield return nested;
            }
        }
    }

    public static bool Bool(string? value, bool fallback = false) => value switch
    {
        null => fallback,
        "1" or "true" or "on" => true,
        "0" or "false" or "off" => false,
        _ => fallback,
    };

    /// <summary>Wartość elementu-przełącznika DrawingML/SpreadsheetML (<c>&lt;b/&gt;</c>, <c>&lt;c:delete val="0"/&gt;</c>): brak elementu = fałsz, brak <c>val</c> = prawda.</summary>
    public static bool Flag(this XElement? element) => element is not null && Bool(element.Attr("val"), true);

    public static int? Int(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;

    public static double? Dbl(string? value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)
        && double.IsFinite(result) ? result : null;

    public static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
