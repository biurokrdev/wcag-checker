namespace D2.Wcag;

public enum IssueSeverity
{
    /// <summary>Informacja dla osoby sprawdzającej; nie wpływa na wynik.</summary>
    Info,

    /// <summary>Warto poprawić; dokument może być mimo to uznany za poprawny.</summary>
    Warning,

    /// <summary>Dokument nie spełnia wymagania; wynik jest niepoprawny.</summary>
    Error,
}

public enum IssueCategory
{
    Document,

    Images,

    Headings,

    Tables,

    Lists,

    Hyperlinks,

    Text,

    Structure,

    Metadata,

    Forms,

    Quality,
}

public sealed record WcagIssue(string Code, IssueSeverity Severity, IssueCategory Category, string Message)
{
    /// <summary>Klucz standardu, którego reguła zgłosiła problem, np. <c>firmowy/2.5</c>; ustawia go walidator.</summary>
    public string? Standard { get; init; }

    /// <summary>
    /// Wystąpienia: każde osobno (każdy obraz, akapit, fragment tekstu), żeby dało się je wskazać w dokumencie.
    /// Pusta lista = zgłoszenie dotyczy całego dokumentu.
    /// </summary>
    public IReadOnlyList<IssueLocation> Locations { get; init; } = [];

    /// <summary>Kopia zgłoszenia z wystąpieniami (puste pomijane).</summary>
    public WcagIssue At(params IssueLocation?[] locations) => this with { Locations = locations.OfType<IssueLocation>().ToList() };

    public WcagIssue At(IEnumerable<IssueLocation?> locations) => this with { Locations = locations.OfType<IssueLocation>().ToList() };
}

public static class IssueCodes
{
    public const string InvalidDocument = "INVALID_DOCUMENT";

    public const string ImageMissingAltText = "IMAGE_MISSING_ALT_TEXT";

    public const string ImageAltTextPlaceholder = "IMAGE_ALT_TEXT_PLACEHOLDER";

    public const string ContentBeforeHeading = "CONTENT_BEFORE_HEADING";

    public const string EmptyHeading = "EMPTY_HEADING";

    public const string HeadingLevelSkipped = "HEADING_LEVEL_SKIPPED";

    public const string HeadingTooLong = "HEADING_TOO_LONG";

    public const string FakeHeading = "FAKE_HEADING";

    public const string TableHeaderCellEmpty = "TABLE_HEADER_CELL_EMPTY";

    public const string TableMergedCells = "TABLE_MERGED_CELLS";

    public const string TextLowContrast = "TEXT_LOW_CONTRAST";

    public const string RepeatedBlanks = "REPEATED_BLANKS";

    public const string TocMissing = "TOC_MISSING";

    public const string LanguageInvalid = "LANGUAGE_INVALID";

    public const string TitleGeneric = "TITLE_GENERIC";

    public const string TableLayoutSuspected = "TABLE_LAYOUT_SUSPECTED";

    public const string HyperlinkAmbiguous = "HYPERLINK_AMBIGUOUS";

    public const string JustifiedText = "JUSTIFIED_TEXT";

    public const string TrackedChangesPresent = "TRACKED_CHANGES_PRESENT";

    public const string CommentsPresent = "COMMENTS_PRESENT";

    public const string TableMissingHeaders = "TABLE_MISSING_HEADERS";

    public const string TableStructureInvalid = "TABLE_STRUCTURE_INVALID";

    public const string TableIrregular = "TABLE_IRREGULAR";

    public const string TableCaptionMissing = "TABLE_CAPTION_MISSING";

    public const string NestedTable = "NESTED_TABLE";

    public const string ListMissingSemantic = "LIST_MISSING_SEMANTIC";

    public const string InvalidHyperlink = "INVALID_HYPERLINK";

    public const string CapsTextPresent = "CAPS_TEXT_PRESENT";

    public const string HiddenTextLowContrast = "HIDDEN_TEXT_LOW_CONTRAST";

    public const string ManualTabs = "MANUAL_TABS";

    public const string DocumentMissingSections = "DOCUMENT_MISSING_SECTIONS";

    public const string FloatingElementsPresent = "FLOATING_ELEMENTS_PRESENT";

    public const string UnclearReadingOrder = "UNCLEAR_READING_ORDER";

    public const string UnstructuredContent = "UNSTRUCTURED_CONTENT";

    public const string FooterMissing = "FOOTER_MISSING";

    public const string PageNumbersMissing = "PAGE_NUMBERS_MISSING";

    public const string TitleMissing = "TITLE_MISSING";

    public const string TitlePhraseMissing = "TITLE_PHRASE_MISSING";

    public const string AuthorMissing = "AUTHOR_MISSING";

    public const string AuthorPhraseMissing = "AUTHOR_PHRASE_MISSING";

    public const string LanguageMissing = "LANGUAGE_MISSING";

    public const string FormFieldLabelMissing = "FORM_FIELD_LABEL_MISSING";

    public const string MarginsMissing = "MARGINS_MISSING";

    public const string InvalidMargins = "INVALID_MARGINS";

    public const string PageFormatNotA4 = "PAGE_FORMAT_NOT_A4";

    public const string UnexpectedFont = "UNEXPECTED_FONT";

    public const string FontTooSmall = "FONT_TOO_SMALL";

    public const string TextColorNotAllowed = "TEXT_COLOR_NOT_ALLOWED";

    public const string ManualCheckSuggested = "MANUAL_CHECK_SUGGESTED";
}
