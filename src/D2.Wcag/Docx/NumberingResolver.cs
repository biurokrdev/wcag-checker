using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

// Numeracja po rozwiązaniu: w:num → w:abstractNum (także przez w:numStyleLink → styl numeracji → w:num → abstractNum
// z w:styleLink), poziom z w:lvlOverride instancji albo z definicji. Poziom listy ze stylu akapitu: w:numPr/w:ilvl
// stylu albo poziom definicji, który wskazuje ten styl przez w:pStyle.
internal sealed class NumberingResolver(MainDocumentPart main, Func<string, Style?> findStyle)
{
    private const int MaxLinkDepth = 4;

    private readonly Numbering? _numbering = main.NumberingDefinitionsPart?.Numbering;

    public ResolvedNumbering? Resolve(int numId, int level, bool fromStyle)
    {
        if (numId <= 0 || Instance(numId) is not { } instance)
        {
            return null;
        }

        var definition = Definition(instance);
        var levelOverride = instance.Elements<LevelOverride>().FirstOrDefault(o => o.LevelIndex?.Value == level);
        var levelElement = levelOverride?.Level ?? definition?.Elements<Level>().FirstOrDefault(l => l.LevelIndex?.Value == level);
        return new ResolvedNumbering(
            numId,
            level,
            definition?.AbstractNumberId?.Value,
            levelElement?.NumberingFormat?.Val?.InnerText,
            levelElement?.LevelText?.Val?.Value,
            levelOverride?.StartOverrideNumberingValue?.Val?.Value ?? levelElement?.StartNumberingValue?.Val?.Value,
            fromStyle,
            levelOverride is not null,
            levelElement);
    }

    /// <summary>Poziom definicji numeracji, który wskazuje jeden z podanych stylów akapitu (w:lvl/w:pStyle); pierwszeństwo ma styl najbliższy akapitowi.</summary>
    public int? LevelOfStyle(int numId, IEnumerable<string> styleIdsNearestFirst)
    {
        if (Instance(numId) is not { } instance || Definition(instance) is not { } definition)
        {
            return null;
        }

        foreach (var styleId in styleIdsNearestFirst)
        {
            var level = definition.Elements<Level>().FirstOrDefault(l => string.Equals(l.ParagraphStyleIdInLevel?.Val?.Value, styleId, StringComparison.Ordinal));
            if (level?.LevelIndex?.Value is { } index)
            {
                return index;
            }
        }

        return null;
    }

    private NumberingInstance? Instance(int numId) =>
        _numbering?.Elements<NumberingInstance>().FirstOrDefault(n => n.NumberID?.Value == numId);

    private AbstractNum? Abstract(int abstractNumId) =>
        _numbering?.Elements<AbstractNum>().FirstOrDefault(a => a.AbstractNumberId?.Value == abstractNumId);

    private AbstractNum? Definition(NumberingInstance instance)
    {
        var definition = instance.AbstractNumId?.Val?.Value is { } id ? Abstract(id) : null;
        for (var depth = 0; depth < MaxLinkDepth && definition?.NumberingStyleLink?.Val?.Value is { } link; depth++)
        {
            var linkedNumId = findStyle(link)?.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
            var linked = linkedNumId is { } linkedId && Instance(linkedId)?.AbstractNumId?.Val?.Value is { } linkedAbstract ? Abstract(linkedAbstract) : null;
            if (linked is null || ReferenceEquals(linked, definition))
            {
                break;
            }

            definition = linked;
        }

        return definition;
    }
}
