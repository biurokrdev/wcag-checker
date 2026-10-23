using D2.Wcag.Docx;
using D2.Wcag.Rules;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Fixes;

// Akapity z ręcznym wypunktowaniem dostają prawdziwą listę: definicję numerowania (wypunktowanie albo numerację
// zależnie od prefiksu) i właściwości listy, a znak wypunktowania znika z tekstu. Kolejne akapity numerowane
// dzielą jedną instancję, żeby numeracja była ciągła; nowa grupa zaczyna od 1.
internal sealed class ListFix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.ListMissingSemantic];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var paragraphs = document.Paragraphs.Where(p => ListRules.LooksLikeManualListItem(p, document)).ToList();
        if (paragraphs.Count == 0)
        {
            yield break;
        }

        var numbering = new NumberingBuilder(document.Main);
        int? bulletNumId = null;
        int? currentNumberedId = null;
        Paragraph? previous = null;

        foreach (var paragraph in paragraphs)
        {
            var match = ListRules.MatchPrefix(paragraph.InnerText);
            int numId;
            if (ListRules.IsNumberedPrefix(match.Value))
            {
                if (currentNumberedId is null || previous is null || !ReferenceEquals(paragraph.PreviousSibling(), previous))
                {
                    currentNumberedId = numbering.CreateInstance(numbered: true);
                }

                numId = currentNumberedId.Value;
            }
            else
            {
                bulletNumId ??= numbering.CreateInstance(numbered: false);
                numId = bulletNumId.Value;
            }

            RemovePrefix(paragraph, match.Length);
            paragraph.ParagraphProperties ??= new ParagraphProperties();
            paragraph.ParagraphProperties.NumberingProperties = new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numId });
            previous = paragraph;
        }

        yield return new AppliedFix(
            IssueCodes.ListMissingSemantic,
            $"Zamieniono {Polish.Count(paragraphs.Count, "akapit", "akapity", "akapitów")} z ręcznym wypunktowaniem na listę; znaki wypunktowania usunięto z tekstu.");
    }

    private static void RemovePrefix(Paragraph paragraph, int length)
    {
        var remaining = length;
        foreach (var text in paragraph.Descendants<Text>().ToList())
        {
            if (remaining == 0)
            {
                break;
            }

            var value = text.Text;
            if (value.Length <= remaining)
            {
                remaining -= value.Length;
                text.Text = string.Empty;
            }
            else
            {
                text.Text = value[remaining..];
                remaining = 0;
            }

            if (text.Text.Length > 0 && (text.Text[0] == ' ' || text.Text[^1] == ' '))
            {
                text.Space = SpaceProcessingModeValues.Preserve;
            }
        }
    }

    private sealed class NumberingBuilder
    {
        private readonly Numbering _numbering;
        private int? _bulletAbstractId;
        private int? _numberedAbstractId;

        public NumberingBuilder(MainDocumentPart main)
        {
            var part = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
            part.Numbering ??= new Numbering();
            _numbering = part.Numbering;
        }

        public int CreateInstance(bool numbered)
        {
            var abstractId = numbered
                ? _numberedAbstractId ??= AddAbstract(numbered: true)
                : _bulletAbstractId ??= AddAbstract(numbered: false);

            var numId = (_numbering.Elements<NumberingInstance>().Max(n => (int?)n.NumberID?.Value) ?? 0) + 1;
            _numbering.Append(new NumberingInstance(new AbstractNumId { Val = abstractId }) { NumberID = numId });
            return numId;
        }

        private int AddAbstract(bool numbered)
        {
            var level = new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = numbered ? NumberFormatValues.Decimal : NumberFormatValues.Bullet },
                new LevelText { Val = numbered ? "%1." : "•" },
                new LevelJustification { Val = LevelJustificationValues.Left },
                new PreviousParagraphProperties(new Indentation { Left = "720", Hanging = "360" }))
            {
                LevelIndex = 0,
            };

            var abstractId = (_numbering.Elements<AbstractNum>().Max(a => (int?)a.AbstractNumberId?.Value) ?? -1) + 1;
            var abstractNum = new AbstractNum(level) { AbstractNumberId = abstractId };

            // Schemat wymaga wszystkich abstractNum przed pierwszym num.
            var firstInstance = _numbering.Elements<NumberingInstance>().FirstOrDefault();
            if (firstInstance is null)
            {
                _numbering.Append(abstractNum);
            }
            else
            {
                _numbering.InsertBefore(abstractNum, firstInstance);
            }

            return abstractId;
        }
    }
}
