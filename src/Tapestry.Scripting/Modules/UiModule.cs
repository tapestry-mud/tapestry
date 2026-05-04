using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine.Ui;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class UiModule : IJintApiModule
{
    private readonly PanelRenderer _renderer;

    public UiModule(PanelRenderer renderer)
    {
        _renderer = renderer;
    }

    public string Namespace => "ui";

    public object Build(JintEngine engine)
    {
        return new
        {
            panel = new Func<JsValue, string>(RenderPanel),
            help = new Func<JsValue, string>(RenderHelp)
        };
    }

    private string RenderPanel(JsValue specVal)
    {
        if (specVal is not ObjectInstance spec)
        {
            throw new ArgumentException("tapestry.ui.panel: argument must be an object");
        }

        var widthVal = spec.Get("width");
        var width = widthVal.Type == Types.Undefined ? 80 : (int)(double)widthVal.ToObject()!;

        var sectionsRaw = spec.Get("sections");
        if (sectionsRaw is not JsArray sectionsArr)
        {
            throw new ArgumentException("tapestry.ui.panel: 'sections' must be an array");
        }

        var sections = new List<Section>();
        for (uint i = 0; i < sectionsArr.Length; i++)
        {
            var sectionVal = sectionsArr[i];
            if (sectionVal is not ObjectInstance sectionObj)
            {
                throw new ArgumentException($"tapestry.ui.panel: section[{i}] must be an object");
            }

            var separatorAbove = i == 0 ? RuleStyle.None : ParseRuleStyle(sectionObj.Get("separatorAbove"), i);

            var rowsRaw = sectionObj.Get("rows");
            if (rowsRaw is not JsArray rowsArr)
            {
                throw new ArgumentException($"tapestry.ui.panel: section[{i}] 'rows' must be an array");
            }

            var rows = new List<Row>();
            for (uint j = 0; j < rowsArr.Length; j++)
            {
                var rowVal = rowsArr[j];
                if (rowVal is not ObjectInstance rowObj)
                {
                    throw new ArgumentException($"tapestry.ui.panel: row at [{i}][{j}] must be an object");
                }
                rows.Add(ParseRow(rowObj, i, j));
            }

            sections.Add(new Section { Rows = rows, SeparatorAbove = separatorAbove });
        }

        return _renderer.Render(new Panel { Width = width, Sections = sections });
    }

    private static RuleStyle ParseRuleStyle(JsValue val, uint si)
    {
        if (val.Type == Types.Undefined) { return RuleStyle.Minor; }
        return val.ToString() switch
        {
            "none" => RuleStyle.None,
            "minor" => RuleStyle.Minor,
            "major" => RuleStyle.Major,
            var s => throw new ArgumentException($"tapestry.ui.panel: unknown separatorAbove '{s}' in section[{si}]")
        };
    }

    private static Row ParseRow(ObjectInstance row, uint si, uint ri)
    {
        var typeVal = row.Get("type");
        if (typeVal.Type == Types.Undefined)
        {
            throw new ArgumentException($"tapestry.ui.panel: row at [{si}][{ri}] missing 'type'");
        }

        return typeVal.ToString() switch
        {
            "empty" => new EmptyRow(),
            "title" => ParseTitleRow(row, si, ri),
            "text" => ParseTextRow(row, si, ri),
            "cell" => ParseCellRow(row, si, ri),
            "footer" => ParseFooterRow(row, si, ri),
            var t => throw new ArgumentException($"tapestry.ui.panel: unknown row type '{t}' at [{si}][{ri}]")
        };
    }

    private static TitleRow ParseTitleRow(ObjectInstance row, uint si, uint ri)
    {
        var leftVal = row.Get("left");
        if (leftVal.Type == Types.Undefined)
        {
            throw new ArgumentException($"tapestry.ui.panel: title row at [{si}][{ri}] missing 'left'");
        }
        var rightVal = row.Get("right");
        return new TitleRow
        {
            Left = leftVal.ToString(),
            Right = rightVal.Type == Types.Undefined || rightVal.Type == Types.Null ? null : rightVal.ToString()
        };
    }

    private static TextRow ParseTextRow(ObjectInstance row, uint si, uint ri)
    {
        var contentVal = row.Get("content");
        if (contentVal.Type == Types.Undefined)
        {
            throw new ArgumentException($"tapestry.ui.panel: text row at [{si}][{ri}] missing 'content'");
        }
        var wrapVal = row.Get("wrap");
        return new TextRow
        {
            Content = contentVal.ToString(),
            Align = ParseAlign(row.Get("align")),
            Wrap = wrapVal.Type == Types.Boolean && (bool)wrapVal.ToObject()!
        };
    }

    private static CellRow ParseCellRow(ObjectInstance row, uint si, uint ri)
    {
        var cellsRaw = row.Get("cells");
        if (cellsRaw is not JsArray cellsArr)
        {
            throw new ArgumentException($"tapestry.ui.panel: cell row at [{si}][{ri}] 'cells' must be an array");
        }

        var cells = new List<Cell>();
        for (uint k = 0; k < cellsArr.Length; k++)
        {
            var cellVal = cellsArr[k];
            if (cellVal is not ObjectInstance cellObj)
            {
                throw new ArgumentException($"tapestry.ui.panel: cell[{k}] at [{si}][{ri}] must be an object");
            }

            var widthVal = cellObj.Get("width");
            if (widthVal.Type == Types.Undefined)
            {
                throw new ArgumentException($"tapestry.ui.panel: cell[{k}] at [{si}][{ri}] missing 'width'");
            }

            var cellWidth = widthVal.Type == Types.String && widthVal.ToString() == "fill"
                ? CellWidth.Fill
                : CellWidth.Fixed((int)(double)widthVal.ToObject()!);

            var cellTypeVal = cellObj.Get("type");
            if (cellTypeVal.Type != Types.Undefined && cellTypeVal.ToString() == "progress")
            {
                var valueVal = cellObj.Get("value");
                if (valueVal.Type == Types.Undefined)
                {
                    throw new ArgumentException($"tapestry.ui.panel: progress cell[{k}] at [{si}][{ri}] missing 'value'");
                }
                var maxVal = cellObj.Get("max");
                if (maxVal.Type == Types.Undefined)
                {
                    throw new ArgumentException($"tapestry.ui.panel: progress cell[{k}] at [{si}][{ri}] missing 'max'");
                }
                cells.Add(new ProgressCell
                {
                    Value = (long)(double)valueVal.ToObject()!,
                    Max = (long)(double)maxVal.ToObject()!,
                    Width = cellWidth
                });
            }
            else
            {
                var contentVal = cellObj.Get("content");
                if (contentVal.Type == Types.Undefined)
                {
                    throw new ArgumentException($"tapestry.ui.panel: cell[{k}] at [{si}][{ri}] missing 'content'");
                }
                cells.Add(new Cell
                {
                    Content = contentVal.ToString(),
                    Width = cellWidth,
                    Align = ParseAlign(cellObj.Get("align"))
                });
            }
        }

        var dividersVal = row.Get("dividers");
        return new CellRow
        {
            Cells = cells,
            ShowDividers = dividersVal.Type == Types.Boolean && (bool)dividersVal.ToObject()!
        };
    }

    private static FooterRow ParseFooterRow(ObjectInstance row, uint si, uint ri)
    {
        var contentVal = row.Get("content");
        if (contentVal.Type == Types.Undefined)
        {
            throw new ArgumentException($"tapestry.ui.panel: footer row at [{si}][{ri}] missing 'content'");
        }
        return new FooterRow { Content = contentVal.ToString() };
    }

    private static Align ParseAlign(JsValue val)
    {
        if (val.Type == Types.Undefined) { return Align.Left; }
        return val.ToString() switch
        {
            "right" => Align.Right,
            "center" => Align.Center,
            _ => Align.Left
        };
    }

    private static string RenderHelp(JsValue specVal)
    {
        if (specVal is not ObjectInstance spec) { return ""; }

        var status = spec.Get("status");
        if (status.Type == Types.Undefined) { return ""; }

        return status.ToString() switch
        {
            "ok" => RenderHelpTopic(spec),
            "multiple" => RenderHelpDisambiguation(spec),
            "no_match" => HelpRenderer.RenderNoMatch(spec.Get("term")?.ToString() ?? ""),
            _ => ""
        };
    }

    private static string RenderHelpTopic(ObjectInstance spec)
    {
        var topicObj = spec.Get("topic");
        if (topicObj is not ObjectInstance t) { return ""; }

        var topic = new Tapestry.Shared.Help.HelpTopic
        {
            Title = t.Get("title")?.ToString() ?? "",
            Brief = t.Get("brief")?.ToString() ?? "",
            Body = t.Get("body")?.ToString() ?? "",
            Syntax = GetStringList(t, "syntax"),
            SeeAlso = GetStringList(t, "seeAlso")
        };
        return HelpRenderer.RenderTopic(topic);
    }

    private static string RenderHelpDisambiguation(ObjectInstance spec)
    {
        var term = spec.Get("term")?.ToString() ?? "";
        var matchesArr = spec.Get("matches");
        var matches = new List<Tapestry.Shared.Help.HelpTopicSummary>();

        if (matchesArr is JsArray arr)
        {
            for (uint i = 0; i < arr.Length; i++)
            {
                if (arr[i] is ObjectInstance m)
                {
                    matches.Add(new Tapestry.Shared.Help.HelpTopicSummary
                    {
                        Id = m.Get("id")?.ToString() ?? "",
                        Title = m.Get("title")?.ToString() ?? "",
                        Brief = m.Get("brief")?.ToString() ?? ""
                    });
                }
            }
        }

        return HelpRenderer.RenderDisambiguation(term, matches);
    }

    private static List<string> GetStringList(ObjectInstance obj, string key)
    {
        var val = obj.Get(key);
        if (val is not JsArray arr) { return new(); }
        var result = new List<string>();
        for (uint i = 0; i < arr.Length; i++)
        {
            result.Add(arr[i].ToString());
        }
        return result;
    }
}
