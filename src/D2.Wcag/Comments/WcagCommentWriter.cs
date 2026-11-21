using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

/// <summary>
/// Zapis zgłoszeń jako komentarzy Worda. Bezstanowy. Przebieg: otwarcie tych samych bajtów tylko do odczytu →
/// rozwiązanie WSZYSTKICH wystąpień do elementów → dopiero potem wstawienie znaczników w modelu części głównej →
/// złożenie nowego pakietu (<see cref="CommentPackage"/>). Runów nie dzieli: zakres kotwiczy się na granicach runów
/// albo na całym akapicie.
/// </summary>
public sealed class WcagCommentWriter : IWcagCommentWriter
{
    private static readonly byte[] CompoundFileHeader = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public CommentedDocument Annotate(byte[] docx, WcagValidationResult result, CommentOptions options)
    {
        ArgumentNullException.ThrowIfNull(docx);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        if (!result.IsDocumentReadable)
        {
            throw new CommentExportException(result.Summary);
        }

        var effective = options.StandardNames is null
            ? options with { StandardNames = result.Standards.Select(s => s.Name).ToList() }
            : options;
        return Annotate(docx, result.Issues, effective);
    }

    public CommentedDocument Annotate(byte[] docx, IEnumerable<WcagIssue> issues, CommentOptions options)
    {
        ArgumentNullException.ThrowIfNull(docx);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(options);
        var issueList = issues.ToList();

        if (docx.Length == 0)
        {
            throw new CommentExportException("Plik jest pusty.");
        }

        if (docx.AsSpan().StartsWith(CompoundFileHeader))
        {
            throw new CommentExportException(
                "Plik jest zaszyfrowany (chroniony hasłem do otwarcia) albo zapisany w starym formacie .doc. Zdejmij hasło lub zapisz plik jako dokument programu Word (.docx) i sprawdź go ponownie.");
        }

        using var stream = new MemoryStream(docx, writable: false);
        if (!DocxOpener.TryOpen(stream, editable: false, out var document, out var error))
        {
            throw new CommentExportException(error!);
        }

        using (document!.Package)
        {
            EnsureSupported(document.Package);
            try
            {
                return Annotate(docx, document, issueList, options);
            }
            catch (Exception ex) when (ex is not CommentExportException && DocxOpener.IsFormatError(ex))
            {
                throw new CommentExportException($"Nie udało się dopisać komentarzy – plik jest uszkodzony albo ma nietypową budowę: {ex.Message}", ex);
            }
        }
    }

    // Kopia zachowuje typ oryginału (dokument, szablon, z makrami): typ zawartości części głównej nie jest zmieniany,
    // a wynik niesie rozszerzenie i typ MIME, pod którymi Word go otworzy. Strict Open XML zostaje odmową, bo SDK
    // tłumaczy przestrzenie nazw przy odczycie i część główna po zapisie nie pasowałaby do reszty pakietu.
    private static void EnsureSupported(WordprocessingDocument package)
    {
        if (package.StrictRelationshipFound)
        {
            throw new CommentExportException(
                "Plik jest zapisany w formacie Strict Open XML. Kopię z komentarzami można przygotować tylko dla pliku w zwykłym formacie Worda – zapisz plik w Wordzie jako „Dokument programu Word (*.docx)” albo „Szablon programu Word (*.dotx)” i sprawdź go ponownie.");
        }
    }

    private static (string Extension, string MediaType) FileType(WordprocessingDocument package) => package.DocumentType switch
    {
        WordprocessingDocumentType.Template => (".dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template"),
        WordprocessingDocumentType.MacroEnabledDocument => (".docm", "application/vnd.ms-word.document.macroEnabled.12"),
        WordprocessingDocumentType.MacroEnabledTemplate => (".dotm", "application/vnd.ms-word.template.macroEnabled.12"),
        _ => (".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
    };

    private static CommentedDocument Annotate(byte[] docx, DocxDocument document, List<WcagIssue> issues, CommentOptions options)
    {
        var main = document.Main;
        var body = document.Body;

        // ---- 1. Plan: wszystkie wystąpienia rozwiązane do elementów, zanim cokolwiek zostanie zmienione.
        var locator = new AnchorLocator(document);
        var whole = locator.FirstCommentable()
            ?? throw new CommentExportException("Dokument nie ma w treści żadnego akapitu, w którym Word pozwala umieścić komentarz.");
        var placements = new List<Placement>();
        var collected = new List<CollectedIssue>();
        foreach (var issue in issues)
        {
            if (issue.Locations.Count == 0)
            {
                collected.Add(new CollectedIssue(issue, null, null));
                continue;
            }

            foreach (var location in issue.Locations)
            {
                var (anchor, note) = locator.Locate(issue, location);
                if (anchor is null)
                {
                    collected.Add(new CollectedIssue(issue, location, note));
                }
                else
                {
                    placements.Add(new Placement(anchor, CommentKind.Occurrence, issue, location, CommentText.ForIssue(issue, note), placements.Count));
                }
            }
        }

        // Kolejność: początek zakresu w dokumencie; komentarze zaczynające się w tym samym miejscu (także akapit
        // i jego pierwszy fragment) – waga malejąco, potem kod; dalej koniec zakresu. Zbiorczy zawsze pierwszy.
        var order = new Dictionary<OpenXmlElement, int>(ReferenceEqualityComparer.Instance);
        foreach (var element in body.Descendants())
        {
            order[element] = order.Count;
        }

        var ordered = placements
            .OrderBy(p => order.GetValueOrDefault(p.Anchor.First ?? p.Anchor.StartParagraph, int.MaxValue))
            .ThenByDescending(p => p.Issue!.Severity)
            .ThenBy(p => p.Issue!.Code, StringComparer.Ordinal)
            .ThenBy(p => order.GetValueOrDefault(p.Anchor.Last ?? p.Anchor.EndParagraph, int.MaxValue))
            .ThenBy(p => p.Sequence)
            .ToList();

        if (collected.Count > 0)
        {
            ordered.Insert(0, new Placement(whole, CommentKind.Collective, null, null, CommentText.Collective(collected), -1));
        }
        else if (ordered.Count == 0)
        {
            ordered.Add(new Placement(whole, CommentKind.NoIssues, null, null, CommentText.NoIssues(options.StandardNames), -1));
        }

        var commentsPart = main.WordprocessingCommentsPart;
        var comments = commentsPart is null ? new Comments() : commentsPart.Comments ?? new Comments();
        var nextId = NextId(comments, body);
        var styles = main.StyleDefinitionsPart?.Styles?.Elements<Style>().ToList() ?? [];
        var textStyle = StyleId(styles, StyleValues.Paragraph, "annotation text");
        var referenceStyle = StyleId(styles, StyleValues.Character, "annotation reference");
        var date = new DateTimeOffset(options.Date.UtcDateTime.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);

        // ---- 2. Mutacja modelu części głównej (pakiet otwarty tylko do odczytu – zmienia się sam model w pamięci).
        var inserter = new MarkerInserter(referenceStyle);
        var written = new List<WrittenComment>();
        foreach (var placement in ordered)
        {
            var id = nextId++;
            inserter.Insert(placement.Anchor, id);
            comments.Append(BuildComment(id, placement.Lines, options, date, textStyle, referenceStyle));
            written.Add(new WrittenComment(
                id, placement.Kind, placement.Issue?.Code, placement.Location?.Key, string.Join("\n", placement.Lines.Select(l => l.Text))));
        }

        // ---- 3. Nowy pakiet: oryginalne wpisy + podmieniona część główna i komentarze.
        var content = CommentPackage.Write(docx, main.Uri, Serialize(main.Document), commentsPart?.Uri, Serialize(comments), date);
        var (extension, mediaType) = FileType(document.Package);
        return new CommentedDocument(content, written, collected.Count, extension, mediaType);
    }

    private static byte[] Serialize(OpenXmlPartRootElement root)
    {
        using var stream = new MemoryStream();
        root.Save(stream);
        return stream.ToArray();
    }

    // Nowe identyfikatory od max(id)+1: liczone po komentarzach i po znacznikach w treści (także osieroconych).
    private static int NextId(Comments comments, Body body)
    {
        var ids = comments.Elements<Comment>().Select(c => c.Id?.Value)
            .Concat(body.Descendants<CommentRangeStart>().Select(m => m.Id?.Value))
            .Concat(body.Descendants<CommentRangeEnd>().Select(m => m.Id?.Value))
            .Concat(body.Descendants<CommentReference>().Select(m => m.Id?.Value));
        var max = -1;
        foreach (var id in ids)
        {
            if (int.TryParse(id, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                max = Math.Max(max, value);
            }
        }

        return max + 1;
    }

    // Style komentarzy są używane tylko wtedy, gdy dokument już je ma (rozpoznanie po nazwie wbudowanej);
    // styles.xml nie jest zmieniany.
    private static string? StyleId(List<Style> styles, StyleValues type, string name) =>
        styles.FirstOrDefault(s => s.Type?.Value == type && string.Equals(s.StyleName?.Val?.Value, name, StringComparison.OrdinalIgnoreCase))?.StyleId?.Value;

    private static Comment BuildComment(int id, IReadOnlyList<CommentLine> lines, CommentOptions options, DateTimeOffset date, string? textStyle, string? referenceStyle)
    {
        var comment = new Comment
        {
            Id = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Author = options.Author,
            Initials = options.Initials,
            Date = new DateTimeValue(date.UtcDateTime),
        };

        for (var i = 0; i < lines.Count; i++)
        {
            var paragraph = new Paragraph();
            if (textStyle is not null)
            {
                paragraph.Append(new ParagraphProperties(new ParagraphStyleId { Val = textStyle }));
            }

            if (i == 0)
            {
                var mark = new Run();
                if (referenceStyle is not null)
                {
                    mark.Append(new RunProperties(new RunStyle { Val = referenceStyle }));
                }

                mark.Append(new AnnotationReferenceMark());
                paragraph.Append(mark);
            }

            foreach (var (text, bold) in lines[i].Segments)
            {
                if (text.Length == 0)
                {
                    continue;
                }

                var run = new Run();
                if (bold)
                {
                    run.Append(new RunProperties(new Bold()));
                }

                run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                paragraph.Append(run);
            }

            comment.Append(paragraph);
        }

        return comment;
    }

    /// <summary>
    /// Kotwica komentarza w treści głównej. <paramref name="First"/> / <paramref name="Last"/>: element, przed którym
    /// staje początek zakresu i za którym staje koniec; <c>null</c> = początek / koniec akapitu (początek tylko
    /// w akapicie bez treści – inaczej cały akapit zaczyna się przed swoim pierwszym elementem treści).
    /// </summary>
    private sealed record Anchor(Paragraph StartParagraph, OpenXmlElement? First, Paragraph EndParagraph, OpenXmlElement? Last);

    private sealed record Placement(Anchor Anchor, CommentKind Kind, WcagIssue? Issue, IssueLocation? Location, IReadOnlyList<CommentLine> Lines, int Sequence);

    // Wystąpienie → kotwica w treści głównej (albo brak kotwicy = komentarz zbiorczy) i dopisek o miejscu.
    private sealed class AnchorLocator(DocxDocument document)
    {
        // Właściwości kontrolki treści (dzieci w:sdtPr), przy których Word pozwala komentować jej wnętrze: kontrolka
        // tekstu sformatowanego, grupa, blok konstrukcyjny, cytat, równanie, bibliografia. Każda inna właściwość
        // (w:text, w:date, w:dropDownList, w:comboBox, w:picture, w14:checkbox, powiązanie danych, symbol zastępczy,
        // nieznane rozszerzenie) oznacza kontrolkę „zamkniętą”: znacznik komentarza w jej w:sdtContent daje plik,
        // którego Word nie otwiera („Plik jest prawdopodobnie uszkodzony”).
        private static readonly HashSet<string> OpenControlProperties = new(StringComparer.Ordinal)
        {
            "rPr", "alias", "tag", "id", "lock", "placeholder", "temporary", "color", "appearance", "label", "tabIndex",
            "richText", "group", "citation", "equation", "bibliography", "docPartObj", "docPartList",
        };

        private List<Paragraph>? _paragraphs;
        private Dictionary<Paragraph, int>? _paragraphIndex;

        /// <summary>Kotwica „cały pierwszy akapit dokumentu, w którym można umieścić komentarz” (zbiorczy, „Brak uwag”).</summary>
        public Anchor? FirstCommentable() => Paragraphs().FirstOrDefault(IsCommentable) is { } first ? Whole(first) : null;

        public (Anchor? Anchor, string? Note) Locate(WcagIssue issue, IssueLocation location)
        {
            var (anchor, note) = Find(issue, location);
            if (anchor is null || (IsCommentable(anchor.StartParagraph) && IsCommentable(anchor.EndParagraph)))
            {
                return (anchor, note);
            }

            // Akapit leży w zamkniętej kontrolce treści na poziomie bloku: komentarz staje na najbliższym akapicie
            // poza nią – przed kontrolką, a gdy takiego nie ma, za nią.
            var snippet = string.IsNullOrWhiteSpace(location.Snippet) ? "" : ": " + location.Snippet.Trim();
            var index = ParagraphIndex()[anchor.StartParagraph];
            var paragraphs = Paragraphs();
            for (var i = index - 1; i >= 0; i--)
            {
                if (IsCommentable(paragraphs[i]))
                {
                    return (Whole(paragraphs[i]), note ?? $"Dotyczy kontrolki treści za tym akapitem (Word nie pozwala komentować wnętrza kontrolki tego typu){snippet}");
                }
            }

            for (var i = index + 1; i < paragraphs.Count; i++)
            {
                if (IsCommentable(paragraphs[i]))
                {
                    return (Whole(paragraphs[i]), note ?? $"Dotyczy kontrolki treści przed tym akapitem (Word nie pozwala komentować wnętrza kontrolki tego typu){snippet}");
                }
            }

            return (null, note ?? (snippet.Length == 0 ? null : "Miejsce" + snippet));
        }

        private List<Paragraph> Paragraphs() => _paragraphs ??= document.Body.Descendants<Paragraph>().ToList();

        private Dictionary<Paragraph, int> ParagraphIndex()
        {
            if (_paragraphIndex is null)
            {
                _paragraphIndex = new Dictionary<Paragraph, int>(ReferenceEqualityComparer.Instance);
                foreach (var paragraph in Paragraphs())
                {
                    _paragraphIndex[paragraph] = _paragraphIndex.Count;
                }
            }

            return _paragraphIndex;
        }

        private static bool IsClosedControl(SdtElement control) =>
            control.GetFirstChild<SdtProperties>() is { } properties && properties.ChildElements.Any(p => !OpenControlProperties.Contains(p.LocalName));

        // Akapit, w którym znaczniki komentarza mogą stanąć jako jego dzieci.
        private static bool IsCommentable(Paragraph paragraph) =>
            !IsEmbedded(paragraph) && !paragraph.Ancestors<SdtElement>().Any(IsClosedControl);

        private (Anchor? Anchor, string? Note) Find(WcagIssue issue, IssueLocation location)
        {
            if (location.Kind == LocationKind.Document)
            {
                return (null, null);
            }

            var snippet = string.IsNullOrWhiteSpace(location.Snippet) ? "" : ": " + location.Snippet.Trim();
            if (location.Story == IssueLocation.BodyStory)
            {
                var found = document.Resolve(location) is { } element ? InBody(element, location, snippet) : (null, null);
                if (found.Anchor is null)
                {
                    return (null, snippet.Length == 0 ? null : "Miejsce" + snippet);
                }

                var note = found.Note;
                if (note is null && issue.Scope == IssueScope.Section)
                {
                    note = "Dotyczy całej sekcji, która zaczyna się w tym miejscu.";
                }

                return (found.Anchor, note);
            }

            // Word nie pokazuje komentarzy w nagłówkach, stopkach ani przypisach: kotwica staje w treści.
            var story = $"Dotyczy {document.DescribeStory(location.Story)}{snippet}";
            if (location.Story is "footnotes" or "endnotes")
            {
                return document.NoteReferenceParagraph(location) is { } reference ? (Whole(Outermost(reference)), story) : (null, story);
            }

            var usages = document.StoryUsages(location.Story);
            if (usages.Count == 0)
            {
                // Część osierocona albo nieznana historia.
                return (null, story);
            }

            var usage = usages.FirstOrDefault(u => u.Active) ?? usages[0];
            var sections = document.Sections();
            if (usage.Section < 0 || usage.Section >= sections.Count)
            {
                return (null, story);
            }

            var section = sections[usage.Section];
            var first = section.LastParagraphIndex < section.FirstParagraphIndex
                ? null
                : document.Resolve(new IssueLocation(LocationKind.Paragraph, IssueLocation.BodyStory, section.FirstParagraphIndex, null, "")) as Paragraph;
            return first is null ? (null, story) : (Whole(Outermost(first)), story);
        }

        private (Anchor? Anchor, string? Note) InBody(OpenXmlElement element, IssueLocation location, string snippet)
        {
            // Grafika: kotwicą jest run, w którym leży (także gdy rysunek siedzi w mc:Choice tego runu).
            if (element is Drawing)
            {
                element = element.Ancestors<Run>().FirstOrDefault() ?? (OpenXmlElement?)element.Ancestors<Paragraph>().FirstOrDefault() ?? element;
            }

            // Wnętrze pola tekstowego albo mc:AlternateContent: Word nie pozwala komentować pól tekstowych, a znaczniki
            // w gałęzi mc:Fallback rozjechałyby się z mc:Choice – kotwicą jest akapit, w którym zakotwiczony jest kształt.
            if (IsEmbedded(element))
            {
                if (element.Ancestors<Paragraph>().LastOrDefault() is not { } host)
                {
                    return (null, null);
                }

                var inTextBox = element.Ancestors().Any(a => a.LocalName == "txbxContent");
                return (Whole(host), inTextBox
                    ? $"Dotyczy pola tekstowego zakotwiczonego w tym akapicie (Word nie pozwala komentować wnętrza pola tekstowego){snippet}"
                    : $"Dotyczy elementu osadzonego w tym akapicie{snippet}");
            }

            switch (element)
            {
                case Paragraph paragraph:
                    return (location.Range is { } range ? Ranged(paragraph, range) : Whole(paragraph), null);

                case Table table:
                    var inTable = table.Elements<TableRow>().SelectMany(r => r.Elements<TableCell>()).SelectMany(c => c.Elements<Paragraph>()).FirstOrDefault()
                        ?? table.Descendants<Paragraph>().FirstOrDefault(p => !IsEmbedded(p));
                    return inTable is null ? (null, null) : (Whole(inTable), "Dotyczy całej tabeli (komentarz w jej pierwszej komórce).");

                case TableCell cell:
                    var paragraphs = cell.Elements<Paragraph>().ToList();
                    if (paragraphs.Count == 0)
                    {
                        paragraphs = cell.Descendants<Paragraph>().Where(p => !IsEmbedded(p)).ToList();
                    }

                    return paragraphs.Count == 0 ? (null, null) : (new Anchor(paragraphs[0], Whole(paragraphs[0]).First, paragraphs[^1], null), null);

                default:
                    // Run, łącze albo inny element w akapicie: zakres obejmuje ten element (albo kontener, do którego
                    // środka znaczników wstawiać nie wolno – patrz Hoist).
                    if (element.Ancestors<Paragraph>().FirstOrDefault() is not { } owner)
                    {
                        return (null, null);
                    }

                    var inline = Hoist(element, owner);
                    return (new Anchor(owner, inline, owner, inline), null);
            }
        }

        // Zakres znaków: granice runów, które go obejmują (bez dzielenia runów); gdy się nie da – cały akapit.
        private Anchor Ranged(Paragraph paragraph, TextRange range)
        {
            var map = document.TextMap(paragraph);
            if (map.RunsCovering(range) is not { } covering)
            {
                return Whole(paragraph);
            }

            Run? first = null;
            Run? last = null;
            foreach (var segment in map.Segments)
            {
                if (segment.RunIndex == covering.FirstRun)
                {
                    first ??= segment.Run;
                }

                if (segment.RunIndex == covering.LastRun)
                {
                    last = segment.Run;
                }
            }

            if (first is null || last is null || IsEmbedded(first, paragraph) || IsEmbedded(last, paragraph))
            {
                return Whole(paragraph);
            }

            return new Anchor(paragraph, Hoist(first, paragraph), paragraph, Hoist(last, paragraph));
        }

        // Cały akapit: początek przed pierwszym elementem treści (po w:pPr i po znacznikach zakładek, zakresów
        // i sprawdzania pisowni), koniec na końcu akapitu. Dzięki temu komentarz do akapitu i komentarz do jego
        // pierwszego fragmentu zaczynają się w tym samym miejscu i porządkuje je waga, nie rodzaj kotwicy.
        private static Anchor Whole(Paragraph paragraph) =>
            new(paragraph, paragraph.ChildElements.FirstOrDefault(c => c is not ParagraphProperties && !IsMarker(c)), paragraph, null);

        private static bool IsMarker(OpenXmlElement element) =>
            element is BookmarkStart or BookmarkEnd or ProofError or PermStart or PermEnd or CommentRangeStart or CommentRangeEnd
            || element.LocalName.EndsWith("RangeStart", StringComparison.Ordinal) || element.LocalName.EndsWith("RangeEnd", StringComparison.Ordinal);

        private static Paragraph Outermost(Paragraph paragraph) => paragraph.Ancestors<Paragraph>().LastOrDefault() ?? paragraph;

        private static bool IsEmbedded(OpenXmlElement element, OpenXmlElement? until = null) =>
            element.Ancestors().TakeWhile(a => !ReferenceEquals(a, until)).Any(a => a is AlternateContent || a.LocalName == "txbxContent");

        // Znaczniki zakresu i run z odwołaniem stają obok elementu tylko w kontenerach, w których Word je przyjmuje:
        // akapit, łącze, wstawienie śledzone i wierszowa kontrolka treści „otwarta” (tekst sformatowany – patrz
        // OpenControlProperties). Każdy inny kontener między elementem a akapitem (kontrolka zwykłego tekstu, daty,
        // listy, obrazu, pole proste, usunięcie śledzone, ruby…) staje się kotwicą w całości – znaczniki lądują
        // poza nim, tak jak zapisuje je sam Word.
        private static OpenXmlElement Hoist(OpenXmlElement element, Paragraph owner)
        {
            var current = element;
            foreach (var ancestor in element.Ancestors().TakeWhile(a => !ReferenceEquals(a, owner)))
            {
                var transparent = ancestor switch
                {
                    Hyperlink or InsertedRun => true,
                    SdtRun control => !IsClosedControl(control),
                    SdtContentRun content => content.Parent is SdtRun control && !IsClosedControl(control),
                    _ => false,
                };
                if (!transparent)
                {
                    current = ancestor;
                }
            }

            return current;
        }
    }

    // Wstawianie znaczników. Kilka komentarzy w tym samym miejscu: początki w kolejności komentarzy przed elementem,
    // za elementem pary „koniec zakresu + odwołanie” w tej samej kolejności.
    private sealed class MarkerInserter(string? referenceStyle)
    {
        private readonly Dictionary<OpenXmlElement, OpenXmlElement> _lastAtStart = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<OpenXmlElement, OpenXmlElement> _lastAfter = new(ReferenceEqualityComparer.Instance);

        public void Insert(Anchor anchor, int id)
        {
            var value = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var start = new CommentRangeStart { Id = value };
            var end = new CommentRangeEnd { Id = value };
            var reference = new Run();
            if (referenceStyle is not null)
            {
                reference.Append(new RunProperties(new RunStyle { Val = referenceStyle }));
            }

            reference.Append(new CommentReference { Id = value });

            if (anchor.First is { } first)
            {
                first.InsertBeforeSelf(start);
            }
            else
            {
                var paragraph = anchor.StartParagraph;
                if (_lastAtStart.TryGetValue(paragraph, out var previous))
                {
                    previous.InsertAfterSelf(start);
                }
                else if (paragraph.GetFirstChild<ParagraphProperties>() is { } properties)
                {
                    properties.InsertAfterSelf(start);
                }
                else
                {
                    paragraph.PrependChild(start);
                }

                _lastAtStart[paragraph] = start;
            }

            if (anchor.Last is { } last)
            {
                (_lastAfter.TryGetValue(last, out var previous) ? previous : last).InsertAfterSelf(end);
                end.InsertAfterSelf(reference);
                _lastAfter[last] = reference;
            }
            else
            {
                anchor.EndParagraph.Append(end, reference);
            }
        }
    }
}
