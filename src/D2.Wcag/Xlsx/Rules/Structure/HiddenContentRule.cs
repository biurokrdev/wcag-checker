using System.Globalization;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// XL-HIDDEN (dobra praktyka, bez kryterium): treść, której nie widać – ukryte i bardzo ukryte arkusze z danymi, ukryte
/// wiersze i kolumny z danymi, komórki z treścią i formatem liczbowym „;;;”. Żadne kryterium poziomu A ani AA nie zakazuje
/// ukrywania; zachowanie czytników ekranu wobec ukrytej treści nie jest potwierdzone w źródłach (macierz, rozdz. 4 i 7).
/// <para>
/// Na arkuszu ukrytym zgłaszany jest tylko sam arkusz (bez wierszy, kolumn i formatów). Ukryte wiersze i kolumny bez
/// treści nie są zgłaszane. Zgłoszenia grupowane są w zakresy: kolejne ukryte wiersze (kolumny) z treścią to jedno
/// zgłoszenie, a komórki z formatem „;;;” łączone są w prostokąty.
/// </para>
/// </summary>
internal sealed class HiddenContentRule : IXlsxRule
{
    public const string Code = "XL-HIDDEN";

    private const string Label = "Informacja (" + XlsxFinding.BestPracticeLabel + "): ";

    private const string FixSheet =
        "Jeśli dane mają być dostępne dla odbiorcy: kliknij prawym przyciskiem myszy dowolną kartę arkusza → Odkryj… → wybierz arkusz → OK (arkusz „bardzo ukryty” odkryjesz tylko w edytorze Visual Basic: Alt+F11 → okno Właściwości → Visible). "
        + "Jeśli dane są pomocnicze albo zbędne, zostaw arkusz ukryty lub usuń go i sprawdź, czy formuły, listy i wykresy nadal działają.";

    private const string FixRowsColumns =
        "Jeśli dane mają być dostępne dla odbiorcy: zaznacz wiersze (kolumny) sąsiadujące z ukrytymi → kliknij prawym przyciskiem myszy nagłówek wiersza (kolumny) → Odkryj (albo Narzędzia główne → Format → Ukryj i odkryj). "
        + "Jeśli są pomocnicze, zostaw je ukryte i upewnij się, że widoczna treść jest zrozumiała bez nich.";

    private const string FixFormat =
        "Zaznacz komórki → Ctrl+1 (Formatuj komórki) → karta Liczby → wybierz kategorię Ogólne albo właściwy format zamiast niestandardowego „;;;”. Jeśli wartość ma pozostać niewidoczna, rozważ przeniesienie jej do arkusza pomocniczego.";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Ukryta treść w skoroszycie",
        [],
        XlsxCheckType.Auto,
        FixRowsColumns)
    {
        BestPractice = true,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        bool[]? invisibleStyle = null;
        var formats = workbook.Styles.CellFormats;
        for (var i = 0; i < formats.Count; i++)
        {
            if (IsInvisibleFormat(formats[i].NumberFormat))
            {
                invisibleStyle ??= new bool[formats.Count];
                invisibleStyle[i] = true;
            }
        }

        foreach (var sheet in workbook.Sheets)
        {
            context.ThrowIfCancellationRequested();
            if (sheet.State != XlsxSheetState.Visible)
            {
                if (!sheet.IsEmpty)
                {
                    yield return SheetHit(sheet);
                }

                continue;
            }

            if (sheet.UsedRange is null)
            {
                continue;
            }

            foreach (var hit in CellHits(sheet, invisibleStyle, context))
            {
                yield return hit;
            }
        }
    }

    internal static bool IsInvisibleFormat(string? numberFormat) =>
        numberFormat is not null && numberFormat.Replace(" ", "", StringComparison.Ordinal) == StructureOptions.InvisibleNumberFormat;

    private static XlsxRuleHit SheetHit(XlsxSheet sheet)
    {
        var veryHidden = sheet.State == XlsxSheetState.VeryHidden;
        var content = sheet.Cells.Count(c => c.HasValue || c.HasFormula);
        var evidence = new List<XlsxEvidence>
        {
            new("Stan arkusza", veryHidden ? "bardzo ukryty (veryHidden)" : "ukryty (hidden)"),
            new("Komórki z treścią", Number(content)),
        };
        if (sheet.UsedRange is { } used)
        {
            evidence.Add(new XlsxEvidence("Zakres danych", used.ToString()));
        }

        var objects = sheet.Tables.Count + sheet.Drawings.Count + sheet.Charts.Count + sheet.Comments.Count + sheet.Controls.Count;
        if (objects > 0)
        {
            evidence.Add(new XlsxEvidence("Tabele i obiekty", Number(objects)));
        }

        return new XlsxRuleHit(Code,
            $"{Label}arkusz „{sheet.Name}” jest {(veryHidden ? "bardzo ukryty" : "ukryty")}, a zawiera dane. Sprawdź, czy odbiorca nie potrzebuje tej treści i czy widoczne arkusze są zrozumiałe bez niej.",
            XlsxLocation.OfSheet(sheet))
        {
            Evidence = evidence,
            FixInstruction = FixSheet,
            Weight = Math.Max(1, content),
        };
    }

    private static List<XlsxRuleHit> CellHits(XlsxSheet sheet, bool[]? invisibleStyle, XlsxAuditContext context)
    {
        HashSet<int>? hiddenRows = null;
        foreach (var row in sheet.Rows)
        {
            if (row.Hidden)
            {
                (hiddenRows ??= []).Add(row.Row);
            }
        }

        bool[]? hiddenColumns = null;
        foreach (var column in sheet.Columns)
        {
            if (column.Hidden)
            {
                hiddenColumns ??= new bool[CellRef.MaxColumn + 2];
                for (var c = Math.Max(1, column.Min); c <= Math.Min(CellRef.MaxColumn, column.Max); c++)
                {
                    hiddenColumns[c] = true;
                }
            }
        }

        var hits = new List<XlsxRuleHit>();
        if (hiddenRows is null && hiddenColumns is null && invisibleStyle is null)
        {
            return hits;
        }

        // Wiersz → (pierwsza kolumna, ostatnia kolumna, liczba komórek); kolumna → (pierwszy wiersz, ostatni wiersz, liczba).
        var rowStats = new SortedDictionary<int, (int Min, int Max, int Count)>();
        var columnStats = new SortedDictionary<int, (int Min, int Max, int Count)>();
        var invisibleCells = new List<CellRef>();
        var cells = sheet.Cells;
        for (var i = 0; i < cells.Count; i++)
        {
            if ((i & StructureOptions.CancellationCheckMask) == 0)
            {
                context.ThrowIfCancellationRequested();
            }

            var cell = cells[i];
            if (!cell.HasValue && !cell.HasFormula)
            {
                continue;
            }

            var reference = cell.Ref;
            if (hiddenRows is not null && hiddenRows.Contains(reference.Row))
            {
                rowStats[reference.Row] = rowStats.TryGetValue(reference.Row, out var s)
                    ? (Math.Min(s.Min, reference.Column), Math.Max(s.Max, reference.Column), s.Count + 1)
                    : (reference.Column, reference.Column, 1);
            }

            if (hiddenColumns is not null && hiddenColumns[reference.Column])
            {
                columnStats[reference.Column] = columnStats.TryGetValue(reference.Column, out var s)
                    ? (Math.Min(s.Min, reference.Row), Math.Max(s.Max, reference.Row), s.Count + 1)
                    : (reference.Row, reference.Row, 1);
            }

            // Sama formuła bez wartości nie ma czego ukrywać – format „;;;” zgłaszamy tylko dla komórek z wartością.
            if (invisibleStyle is not null && cell.HasValue && cell.StyleIndex < invisibleStyle.Length && invisibleStyle[cell.StyleIndex])
            {
                invisibleCells.Add(reference);
            }
        }

        foreach (var (first, last, min, max, count) in Runs(rowStats))
        {
            var range = new RangeRef(new CellRef(first, min), new CellRef(last, max));
            var rows = first == last ? Number(first) : $"{Number(first)}–{Number(last)}";
            hits.Add(new XlsxRuleHit(Code,
                $"{Label}{(first == last ? $"ukryty wiersz {rows} zawiera" : $"ukryte wiersze {rows} zawierają")} dane. Sprawdź, czy odbiorca nie potrzebuje tej treści.",
                XlsxLocation.OfRange(sheet, range))
            {
                Evidence = [new XlsxEvidence("Ukryte wiersze z treścią", rows), new XlsxEvidence("Komórki z treścią", Number(count))],
                FixInstruction = FixRowsColumns,
                Weight = count,
            });
        }

        foreach (var (first, last, min, max, count) in Runs(columnStats))
        {
            var range = new RangeRef(new CellRef(min, first), new CellRef(max, last));
            var columns = first == last ? CellRef.ColumnName(first) : $"{CellRef.ColumnName(first)}–{CellRef.ColumnName(last)}";
            hits.Add(new XlsxRuleHit(Code,
                $"{Label}{(first == last ? $"ukryta kolumna {columns} zawiera" : $"ukryte kolumny {columns} zawierają")} dane. Sprawdź, czy odbiorca nie potrzebuje tej treści.",
                XlsxLocation.OfRange(sheet, range))
            {
                Evidence = [new XlsxEvidence("Ukryte kolumny z treścią", columns), new XlsxEvidence("Komórki z treścią", Number(count))],
                FixInstruction = FixRowsColumns,
                Weight = count,
            });
        }

        foreach (var range in XlsxRanges.Group(invisibleCells))
        {
            hits.Add(new XlsxRuleHit(Code,
                $"{Label}{(range.IsSingleCell ? "komórka ma" : "komórki mają")} treść ukrytą formatem liczbowym „;;;” – wartość jest w pliku, ale nie widać jej w arkuszu.",
                XlsxLocation.OfRange(sheet, range, sheet.TextAt(range.From)))
            {
                Evidence = [new XlsxEvidence("Format liczbowy", StructureOptions.InvisibleNumberFormat), new XlsxEvidence("Komórki z treścią", Number(range.Cells))],
                FixInstruction = FixFormat,
                Weight = (int)Math.Min(int.MaxValue, range.Cells),
            });
        }

        return hits;
    }

    /// <summary>Kolejne (sąsiadujące) klucze łączone w ciągi: (pierwszy, ostatni, najmniejsza i największa współrzędna poprzeczna, liczba komórek).</summary>
    private static IEnumerable<(int First, int Last, int Min, int Max, int Count)> Runs(SortedDictionary<int, (int Min, int Max, int Count)> stats)
    {
        var open = false;
        (int First, int Last, int Min, int Max, int Count) run = default;
        foreach (var (key, value) in stats)
        {
            if (open && key == run.Last + 1)
            {
                run = (run.First, key, Math.Min(run.Min, value.Min), Math.Max(run.Max, value.Max), run.Count + value.Count);
                continue;
            }

            if (open)
            {
                yield return run;
            }

            run = (key, key, value.Min, value.Max, value.Count);
            open = true;
        }

        if (open)
        {
            yield return run;
        }
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
