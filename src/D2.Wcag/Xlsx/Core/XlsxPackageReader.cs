using System.Xml;
using D2.Wcag.Packages;

namespace D2.Wcag.Xlsx;

/// <summary>
/// Odczyt skoroszytu OOXML (Transitional i Strict) bez <c>SpreadsheetDocument</c>: <c>ZipArchive</c> + <c>XmlReader</c> z zakazem
/// DTD, limitami z <see cref="XlsxLimits"/> liczonymi na faktycznie rozpakowanych bajtach, limitem czasu i anulowaniem.
/// Relacje rozwiązywane są wyłącznie wewnątrz pakietu; cele zewnętrzne zostają tekstem. Makr nie uruchamiamy, formuł nie
/// przeliczamy.
/// </summary>
internal static class XlsxPackageReader
{
    private static readonly string[] MediaExtensions =
        [".mp3", ".wav", ".wma", ".m4a", ".aac", ".ogg", ".mid", ".midi", ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".mpg", ".mpeg", ".webm"];

    /// <summary>
    /// Czyta skoroszyt. Dla złego pliku rzuca <see cref="XlsxReadException"/> (z powodem i polskim komunikatem);
    /// anulowanie zgłasza <see cref="OperationCanceledException"/>.
    /// </summary>
    public static XlsxWorkbook Read(byte[] content, XlsxLimits? limits = null, CancellationToken cancellationToken = default) =>
        Read(content, new XlsxReadBudget(limits ?? new XlsxLimits(), cancellationToken));

    public static XlsxWorkbook Read(byte[] content, XlsxReadBudget budget)
    {
        try
        {
            using var package = XlsxPackage.Open(content, budget);
            return ReadPackage(package, budget);
        }
        catch (XlsxReadException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (XmlException exception)
        {
            throw new XlsxReadException(XlsxFailure.Corrupted,
                exception.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase)
                    ? "Plik zawiera niedozwoloną deklarację DTD i nie został przeanalizowany."
                    : "Plik jest uszkodzony: jedna z części skoroszytu nie jest poprawnym XML.",
                exception);
        }
        catch (OutOfMemoryException exception)
        {
            throw new XlsxReadException(XlsxFailure.LimitExceeded, "Skoroszyt jest zbyt duży, żeby go przeanalizować.", exception);
        }
        catch (Exception exception)
        {
            // InvalidDataException (uszkodzona kompresja), IOException i każdy inny błąd odczytu złego pliku.
            throw new XlsxReadException(XlsxFailure.Corrupted, "Plik jest uszkodzony: nie udało się odczytać zawartości skoroszytu.", exception);
        }
    }

    private static XlsxWorkbook ReadPackage(XlsxPackage package, XlsxReadBudget budget)
    {
        var packageRelationships = package.Relationships("");
        var workbookPart = packageRelationships.FirstOrDefault(r => r.Type == "officeDocument" && !r.External)?.Target;
        if (workbookPart is null || !package.Has(workbookPart))
        {
            workbookPart = "xl/workbook.xml";
        }

        var workbookRoot = package.LoadXml(workbookPart)
            ?? throw new XlsxReadException(XlsxFailure.NotSpreadsheet, "Plik nie jest skoroszytem Excel (.xlsx): brak części skoroszytu.");
        if (workbookRoot.Name.LocalName != "workbook")
        {
            throw new XlsxReadException(XlsxFailure.NotSpreadsheet, "Plik nie jest skoroszytem Excel (.xlsx): niepoprawna część skoroszytu.");
        }

        var isStrict = workbookRoot.Name.NamespaceName.StartsWith("http://purl.oclc.org/ooxml/", StringComparison.Ordinal)
            || workbookRoot.Attr("conformance") == "strict";
        var relationships = package.Relationships(workbookPart);
        var byId = new Dictionary<string, XlsxRelationship>(StringComparer.Ordinal);
        foreach (var relationship in relationships)
        {
            byId.TryAdd(relationship.Id, relationship);
        }

        string? PartOfType(string type, string fallback)
        {
            var target = relationships.FirstOrDefault(r => r.Type == type && !r.External)?.Target;
            return target is not null && package.Has(target) ? target : package.Has(fallback) ? fallback : null;
        }

        var theme = XlsxPartParsers.ParseTheme(package.LoadXml(PartOfType("theme", "xl/theme/theme1.xml")));
        var styles = XlsxPartParsers.ParseStyles(package.LoadXml(PartOfType("styles", "xl/styles.xml")));
        var shared = XlsxSharedStrings.Empty;
        if (PartOfType("sharedStrings", "xl/sharedStrings.xml") is { } sharedPart)
        {
            using var stream = package.Open(sharedPart)!;
            shared = XlsxSheetParser.ParseSharedStrings(stream, budget);
        }

        var persons = XlsxPartParsers.ParsePersons(package.LoadXml(relationships.FirstOrDefault(r => r.Type == "person" && !r.External)?.Target));
        var drawings = new XlsxDrawingParser(package, theme);
        var hasMacroSheet = false;
        var hasOle = false;

        var sheetElements = workbookRoot.El("sheets").Els("sheet").ToList();
        if (sheetElements.Count > budget.Limits.MaxSheets)
        {
            throw new XlsxReadException(XlsxFailure.LimitExceeded, $"Skoroszyt ma zbyt wiele arkuszy (limit: {budget.Limits.MaxSheets}).");
        }

        var sheets = new List<XlsxSheet>();
        foreach (var element in sheetElements)
        {
            budget.Check();
            var name = element.Attr("name") ?? "";
            var state = element.Attr("state") switch
            {
                "hidden" => XlsxSheetState.Hidden,
                "veryHidden" => XlsxSheetState.VeryHidden,
                _ => XlsxSheetState.Visible,
            };
            byId.TryGetValue(element.RelAttr("id") ?? "", out var relationship);
            var isChartSheet = relationship?.Type == "chartsheet";
            hasMacroSheet |= relationship?.Type is "xlMacrosheet" or "xlIntlMacrosheet";

            XlsxSheetContent content;
            if (relationship is null || relationship.External || !package.Has(relationship.Target)
                || relationship.Type is not ("worksheet" or "chartsheet" or "dialogsheet"))
            {
                content = new XlsxSheetContent();
            }
            else
            {
                content = ReadSheet(package, relationship.Target, shared, styles, theme, drawings, persons, budget, out var sheetHasOle);
                hasOle |= sheetHasOle;
            }

            sheets.Add(new XlsxSheet(name, sheets.Count, state, isChartSheet, content, shared));
        }

        var definedNames = new List<XlsxDefinedName>();
        foreach (var element in workbookRoot.El("definedNames").Els("definedName"))
        {
            if (element.Attr("name") is { Length: > 0 } definedName)
            {
                definedNames.Add(new XlsxDefinedName(definedName, element.Value, XlsxXml.Int(element.Attr("localSheetId")),
                    XlsxXml.Bool(element.Attr("hidden"))));
            }
        }

        var corePart = packageRelationships.FirstOrDefault(r => r.Type == "core-properties" && !r.External)?.Target ?? "docProps/core.xml";
        var appPart = packageRelationships.FirstOrDefault(r => r.Type is "extended-properties" or "extendedProperties" && !r.External)?.Target
            ?? "docProps/app.xml";
        var properties = XlsxPartParsers.ParseProperties(LoadOptional(package, corePart), LoadOptional(package, appPart));

        var hasMacros = hasMacroSheet;
        var hasMedia = hasOle;
        foreach (var partName in package.Names)
        {
            var file = partName[(partName.LastIndexOf('/') + 1)..];
            hasMacros |= file.Equals("vbaProject.bin", StringComparison.OrdinalIgnoreCase);
            hasMedia |= partName.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase)
                || MediaExtensions.Any(extension => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        return new XlsxWorkbook(properties, sheets, definedNames, styles, theme, hasMacros, hasMedia, isStrict);
    }

    /// <summary>Właściwości dokumentu są dodatkiem: nieczytelna część nie blokuje audytu (DTD nadal jest odmową).</summary>
    private static System.Xml.Linq.XElement? LoadOptional(XlsxPackage package, string partName)
    {
        try
        {
            return package.LoadXml(partName);
        }
        catch (XmlException exception) when (!exception.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    private static XlsxSheetContent ReadSheet(XlsxPackage package, string partName, XlsxSharedStrings shared, XlsxStyles styles,
        XlsxTheme theme, XlsxDrawingParser drawings, Dictionary<string, string> persons, XlsxReadBudget budget, out bool hasOle)
    {
        RawSheet raw;
        using (var stream = package.Open(partName)!)
        {
            raw = XlsxSheetParser.Parse(stream, shared, styles, theme, budget);
        }

        hasOle = raw.HasOleObjects;
        var content = raw.Content;
        var relationships = package.Relationships(partName);
        var byId = new Dictionary<string, XlsxRelationship>(StringComparer.Ordinal);
        foreach (var relationship in relationships)
        {
            byId.TryAdd(relationship.Id, relationship);
        }

        // Tekst komórki pod łączem potrzebuje gotowego arkusza – składamy tymczasowy widok na te same tablice.
        var view = new XlsxSheet("", 0, XlsxSheetState.Visible, false, content, shared);

        var hyperlinks = new List<XlsxHyperlink>();
        foreach (var (range, relId, location, display, tooltip) in raw.Hyperlinks)
        {
            XlsxRelationship? target = null;
            if (relId is not null)
            {
                byId.TryGetValue(relId, out target);
            }

            hyperlinks.Add(new XlsxHyperlink(range, target?.Target, location, display, tooltip, target is { External: true },
                view.TextAt(range.From)));
        }

        content.Hyperlinks = hyperlinks;

        var tables = new List<XlsxTable>();
        foreach (var relId in raw.TableRelIds)
        {
            if (byId.TryGetValue(relId, out var relationship) && !relationship.External
                && XlsxPartParsers.ParseTable(package.LoadXml(relationship.Target)) is { } table)
            {
                tables.Add(table);
            }
        }

        content.Tables = tables;

        var drawingObjects = new List<XlsxDrawingObject>();
        if (raw.DrawingRelId is not null && byId.TryGetValue(raw.DrawingRelId, out var drawing) && !drawing.External)
        {
            var (objects, charts) = drawings.ParseDrawing(drawing.Target);
            drawingObjects = objects;
            content.Drawings = objects;
            content.Charts = charts;
        }

        var comments = new List<XlsxComment>();
        foreach (var relationship in relationships.Where(r => !r.External))
        {
            if (relationship.Type == "comments")
            {
                comments.AddRange(XlsxPartParsers.ParseComments(package.LoadXml(relationship.Target)));
            }
            else if (relationship.Type == "threadedComment")
            {
                comments.AddRange(XlsxPartParsers.ParseThreadedComments(package.LoadXml(relationship.Target), persons));
            }
        }

        content.Comments = comments
            .Select((comment, order) => (comment, order))
            .OrderBy(c => c.comment.Ref)
            .ThenBy(c => c.order)
            .Select(c => c.comment)
            .ToList();

        var vmlShapes = raw.LegacyDrawingRelId is not null && byId.TryGetValue(raw.LegacyDrawingRelId, out var vml) && !vml.External
            ? drawings.ParseVml(vml.Target)
            : [];
        content.Controls = ReadControls(package, raw, byId, vmlShapes, drawingObjects);
        return content;
    }

    private static List<XlsxControl> ReadControls(XlsxPackage package, RawSheet raw, Dictionary<string, XlsxRelationship> byId,
        List<VmlShape> vmlShapes, List<XlsxDrawingObject> drawingObjects)
    {
        var controls = new List<XlsxControl>();
        var usedShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var control in raw.Controls)
        {
            XlsxRelationship? relationship = null;
            if (control.RelId is not null)
            {
                byId.TryGetValue(control.RelId, out relationship);
            }

            var shapeId = control.ShapeId is null ? null : "_x0000_s" + control.ShapeId;
            var shape = shapeId is null ? null : vmlShapes.FirstOrDefault(s => s.Id.Equals(shapeId, StringComparison.OrdinalIgnoreCase));
            if (shape is not null)
            {
                usedShapes.Add(shape.Id);
            }

            var isActiveX = relationship?.Type == "control";
            var kind = "ActiveX";
            string? linkedCell = control.LinkedCell ?? shape?.LinkedCell;
            if (!isActiveX)
            {
                var properties = relationship is { External: false, Type: "ctrlProp" } ? package.LoadXml(relationship.Target) : null;
                kind = ControlKind(properties.Attr("objectType") ?? shape?.ObjectType);
                linkedCell ??= XlsxXml.NullIfEmpty(properties.Attr("fmlaLink"));
            }

            var drawingText = drawingObjects.FirstOrDefault(o => o.Type == XlsxDrawingType.Control && o.Name == control.Name)?.Text;
            controls.Add(new XlsxControl(control.Name, kind, isActiveX, control.AltText ?? shape?.AltText,
                isActiveX ? null : shape?.Label ?? XlsxXml.NullIfEmpty(drawingText), control.Anchor ?? shape?.Anchor, linkedCell));
        }

        // Kontrolki zapisane wyłącznie w VML (pliki ze starszych wersji Excela).
        foreach (var shape in vmlShapes)
        {
            if (shape.ObjectType.Equals("Note", StringComparison.OrdinalIgnoreCase) || usedShapes.Contains(shape.Id))
            {
                continue;
            }

            controls.Add(new XlsxControl(shape.Id, ControlKind(shape.ObjectType), false, shape.AltText, shape.Label, shape.Anchor, shape.LinkedCell));
        }

        return controls;
    }

    private static string ControlKind(string? objectType) => objectType?.ToLowerInvariant() switch
    {
        "checkbox" => "CheckBox",
        "button" => "Button",
        "drop" => "ComboBox",
        "list" => "ListBox",
        "radio" => "OptionButton",
        "gbox" => "GroupBox",
        "label" => "Label",
        "scroll" => "ScrollBar",
        "spin" => "Spinner",
        "editbox" => "EditBox",
        "dialog" => "Dialog",
        "pict" => "Picture",
        null or "" => "Unknown",
        _ => objectType,
    };
}
