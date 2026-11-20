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
    /// <summary>Klucz standardu, którego reguła zgłosiła problem, np. <c>firmowy/2.0</c>; ustawia go walidator.</summary>
    public string? Standard { get; init; }

    /// <summary>
    /// Wystąpienia: każde osobno (każdy obraz, akapit, fragment tekstu), żeby dało się je wskazać w dokumencie.
    /// Pusta lista = zgłoszenie dotyczy całego dokumentu.
    /// </summary>
    public IReadOnlyList<IssueLocation> Locations { get; init; } = [];

    /// <summary>
    /// Status wymagania w specyfikacji; <c>null</c> = reguła sprzed kontraktu (waga podana wprost). Gdy ustawiony,
    /// waga odpowiada <see cref="SpecStatuses.Severity"/>.
    /// </summary>
    public SpecStatus? Status { get; init; }

    /// <summary>Tytuł zgłoszenia po polsku; <c>null</c> = <see cref="IssueLabels.Of"/> dla kodu.</summary>
    public string? Title { get; init; }

    /// <summary>Instrukcja naprawy; <c>null</c> = jest częścią <see cref="Message"/>.</summary>
    public string? Fix { get; init; }

    /// <summary>Zakres zgłoszenia podany wprost; <c>null</c> = wyliczany z wystąpienia (<see cref="IssueScopes.Effective"/>).</summary>
    public IssueScope? Scope { get; init; }

    /// <summary>Właściwości, których dotyczy zgłoszenie (wartość, oczekiwana, źródło).</summary>
    public IReadOnlyList<IssueDetail> Details { get; init; } = [];

    /// <summary>
    /// Grupa deduplikacji: zgłoszenia z tą samą grupą i tymi samymi wystąpieniami walidator scala do jednego
    /// (wygrywa pierwszy standard w profilu), nawet gdy mają różne kody.
    /// </summary>
    public string? DedupGroup { get; init; }

    /// <summary>Kopia zgłoszenia z wystąpieniami (puste pomijane).</summary>
    public WcagIssue At(params IssueLocation?[] locations) => this with { Locations = locations.OfType<IssueLocation>().ToList() };

    public WcagIssue At(IEnumerable<IssueLocation?> locations) => this with { Locations = locations.OfType<IssueLocation>().ToList() };
}

public static class IssueScopes
{
    /// <summary>
    /// Zakres zgłoszenia dla jednego wystąpienia: podany wprost albo wyliczony – brak wystąpienia lub cały dokument →
    /// <see cref="IssueScope.Document"/>; historia nagłówka albo stopki → <see cref="IssueScope.HeaderFooter"/>;
    /// fragment tekstu albo zakres znaków → <see cref="IssueScope.Range"/>; pozostałe → <see cref="IssueScope.Paragraph"/>.
    /// </summary>
    public static IssueScope Effective(WcagIssue issue, IssueLocation? location)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (issue.Scope is { } scope)
        {
            return scope;
        }

        if (location is null || location.Kind == LocationKind.Document)
        {
            return IssueScope.Document;
        }

        if (location.Story.StartsWith("header", StringComparison.Ordinal) || location.Story.StartsWith("footer", StringComparison.Ordinal))
        {
            return IssueScope.HeaderFooter;
        }

        return location.Kind == LocationKind.Run || location.Range is not null ? IssueScope.Range : IssueScope.Paragraph;
    }
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

    // Standard pism firmowych 2.0
    public const string JustificationForbidden = "JUSTIFICATION_FORBIDDEN";

    public const string FontFamilyMismatch = "FONT_FAMILY_MISMATCH";

    public const string FontWeightInFamilyName = "FONT_WEIGHT_IN_FAMILY_NAME";

    public const string LineSpacingMismatch = "LINE_SPACING_MISMATCH";

    public const string BodyTextSizeMismatch = "BODY_TEXT_SIZE_MISMATCH";

    public const string HeadingStyleMismatch = "HEADING_STYLE_MISMATCH";

    public const string HeadingSpacingMismatch = "HEADING_SPACING_MISMATCH";

    public const string LinkStyleMismatch = "LINK_STYLE_MISMATCH";

    public const string FooterStyleMismatch = "FOOTER_STYLE_MISMATCH";

    public const string FooterTitleMissing = "FOOTER_TITLE_MISSING";

    public const string TocRequired = "TOC_REQUIRED";

    public const string TocHeadingInToc = "TOC_HEADING_IN_TOC";

    public const string TocHeadingStyleMismatch = "TOC_HEADING_STYLE_MISMATCH";

    public const string ListBulletColorMismatch = "LIST_BULLET_COLOR_MISMATCH";

    public const string ListNumberNotBold = "LIST_NUMBER_NOT_BOLD";

    public const string TableEmptyCell = "TABLE_EMPTY_CELL";

    public const string TableRowCanSplit = "TABLE_ROW_CAN_SPLIT";

    public const string TableBorderMismatch = "TABLE_BORDER_MISMATCH";

    public const string TableHeaderNotBold = "TABLE_HEADER_NOT_BOLD";

    public const string TableAlignmentMismatch = "TABLE_ALIGNMENT_MISMATCH";

    public const string TableTitleMissing = "TABLE_TITLE_MISSING";

    public const string TitleSuffixInvalid = "TITLE_SUFFIX_INVALID";

    public const string AuthorNotInstitution = "AUTHOR_NOT_INSTITUTION";

    public const string VariantMarkerUnbalanced = "VARIANT_MARKER_UNBALANCED";

    public const string VariantMarkerColorMismatch = "VARIANT_MARKER_COLOR_MISMATCH";

    public const string VariableColorMismatch = "VARIABLE_COLOR_MISMATCH";

    public const string VariantInflectionShortcut = "VARIANT_INFLECTION_SHORTCUT";

    public const string SampleMarkStyleMismatch = "SAMPLE_MARK_STYLE_MISMATCH";

    public const string SampleMarkNotOnEveryPage = "SAMPLE_MARK_NOT_ON_EVERY_PAGE";

    public const string TableDescriptionMissing = "TABLE_DESCRIPTION_MISSING";

    public const string TextColorUsageMismatch = "TEXT_COLOR_USAGE_MISMATCH";

    public const string VariantSlashAlternative = "VARIANT_SLASH_ALTERNATIVE";

    public const string TitlePageFooterPresent = "TITLE_PAGE_FOOTER_PRESENT";

    public const string BrandImageMismatch = "BRAND_IMAGE_MISMATCH";

    public const string ManualCheckSuggested = "MANUAL_CHECK_SUGGESTED";
}
