using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// Reguły grupy „treści nietekstowe, łącza, kontrolki, formularze” (X2). Powiązania z kryteriami i typy kontroli wg
/// <c>docs/xlsx-wcag-matrix.md</c> (rozdz. 3.4 i 4); progi i wzorce w <see cref="NonTextOptions"/>.
/// </summary>
internal static class NonTextRules
{
    /// <summary>Nazwa grupy w identyfikatorach pozycji checklisty: <c>manual-{sc}:nontext</c>.</summary>
    public const string Group = "nontext";

    internal static IReadOnlyList<IXlsxRule> All =>
    [
        new AltTextRule(),
        new ChartTitleRule(),
        new TextBoxRule(),
        new LinkRule(),
        new ControlRule(),
        new TextInImageRule(),
        new DataEntryRule(),
    ];

    /// <summary>
    /// Pytanie o to, co w kryterium pozostaje do oceny człowieka, gdy grupa pokrywa je tylko częściowo. Treść pytania
    /// i wskazówka pochodzą z katalogu kryteriów (macierz pkt 6.1), identyfikator: <c>manual-{sc}:nontext</c>.
    /// </summary>
    public static XlsxManualCheck CriterionCheck(string sc)
    {
        var criterion = XlsxWcagCriteria.Find(sc) ?? throw new ArgumentException($"Nieznane kryterium „{sc}”.", nameof(sc));
        return new XlsxManualCheck
        {
            Id = $"manual-{sc}:{Group}",
            Wcag = [criterion.ToRef()],
            Question = criterion.Question ?? throw new InvalidOperationException($"Kryterium {sc} nie ma pytania w katalogu."),
            Hint = criterion.Hint,
        };
    }

    /// <summary>Obiekty rysunku arkusza wraz z informacją odziedziczoną po grupach nadrzędnych.</summary>
    public static IEnumerable<DrawingInfo> Objects(XlsxSheet sheet)
    {
        var byId = new Dictionary<string, DrawingInfo>(StringComparer.Ordinal);
        foreach (var obj in sheet.Drawings)
        {
            DrawingInfo? parent = null;
            if (obj.ParentGroupId is not null)
            {
                byId.TryGetValue(obj.ParentGroupId, out parent);
            }

            var info = new DrawingInfo(
                obj,
                obj.Hidden || parent is { Hidden: true },
                parent is not null && (parent.GroupDescribed || !NonTextOptions.IsBlank(AltOf(parent.Object))),
                parent is not null && (parent.GroupDecorative || parent.Object.Decorative));
            if (obj.IsGroup)
            {
                // Obiekty grupy stoją na liście za grupą; przy powtórzonym identyfikatorze wygrywa ostatnia grupa przed nimi.
                byId[obj.ObjectId] = info;
            }

            yield return info;
        }
    }

    /// <summary>Tekst alternatywny obiektu: opis (<c>descr</c>), a gdy go brak – tytuł (<c>title</c>).</summary>
    public static string? AltOf(XlsxDrawingObject obj) => NonTextOptions.IsBlank(obj.Description) ? obj.Title : obj.Description;

    public static string TypeLabel(XlsxDrawingObject obj) => obj switch
    {
        { IsGroup: true } => "grupa obiektów",
        { Type: XlsxDrawingType.Image } => "obraz",
        { Type: XlsxDrawingType.Chart } => "wykres",
        { Type: XlsxDrawingType.TextBox } => "pole tekstowe",
        { Type: XlsxDrawingType.Control } => "kontrolka",
        _ => "kształt",
    };
}

/// <summary>Obiekt rysunku z cechami odziedziczonymi po grupach.</summary>
/// <param name="Hidden">Obiekt albo któraś z jego grup jest ukryta.</param>
/// <param name="GroupDescribed">Któraś z grup nadrzędnych ma tekst alternatywny (opis grupy obejmuje jej elementy).</param>
/// <param name="GroupDecorative">Któraś z grup nadrzędnych jest oznaczona jako dekoracyjna.</param>
internal sealed record DrawingInfo(XlsxDrawingObject Object, bool Hidden, bool GroupDescribed, bool GroupDecorative);
