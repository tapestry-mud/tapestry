using Jint.Native;
using Jint.Runtime;
using Tapestry.Engine.Help;
using Tapestry.Shared.Help;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class HelpModule : IJintApiModule
{
    private readonly HelpService _helpService;

    public string Namespace => "help";

    public HelpModule(HelpService helpService)
    {
        _helpService = helpService;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            query = new Func<JsValue, JsValue, JsValue>((a, b) => QueryJs(engine, a, b)),
            list = new Func<JsValue, JsValue, JsValue>((a, b) => ListJs(engine, a, b)),
            categories = new Func<JsValue, JsValue>(a => CategoriesJs(engine, a))
        };
    }

    // Exposed for unit tests without a live Jint engine
    public HelpQueryResult QueryDirect(string? entityId, string term)
    {
        return _helpService.Query(entityId, term);
    }

    public List<string> CategoriesDirect(string? entityId)
    {
        return _helpService.Categories(entityId);
    }

    private JsValue QueryJs(JintEngine engine, JsValue first, JsValue second)
    {
        var (entityId, term) = ResolveArgs(first, second);
        if (term == null) { return JsValue.Null; }

        var result = _helpService.Query(entityId, term);
        return BuildResult(engine, result);
    }

    private JsValue ListJs(JintEngine engine, JsValue first, JsValue second)
    {
        var (entityId, category) = ResolveArgs(first, second);
        if (category == null) { return BuildArray(engine, Array.Empty<JsValue>()); }

        var summaries = _helpService.List(entityId, category);
        var items = summaries
            .Select(s => JsValue.FromObject(engine, new { id = s.Id, title = s.Title, brief = s.Brief }))
            .ToArray();
        return BuildArray(engine, items);
    }

    private JsValue CategoriesJs(JintEngine engine, JsValue first)
    {
        var entityId = IsGuid(first) ? first.ToString() : null;
        var cats = _helpService.Categories(entityId);
        return BuildArray(engine, cats.Select(c => JsValue.FromObject(engine, c)).ToArray());
    }

    // Array.Construct passes items as constructor arguments, so a single item hits
    // the new Array(singleArg) path where Jint checks if it is a number (length).
    // JsArray(engine, items) constructs the array directly, bypassing that path.
    private static JsValue BuildArray(JintEngine engine, JsValue[] items)
    {
        return new Jint.Native.JsArray(engine, items);
    }

    private static JsValue BuildResult(JintEngine engine, HelpQueryResult result)
    {
        object payload = result.Status switch
        {
            "ok" => new
            {
                status = "ok",
                topic = result.Topic == null ? null : TopicToAnon(result.Topic)
            },
            "multiple" => new
            {
                status = "multiple",
                term = result.Term,
                matches = result.Matches?.Select(m => new { id = m.Id, title = m.Title, brief = m.Brief }).ToArray()
            },
            _ => (object)new { status = "no_match", term = result.Term }
        };
        return JsValue.FromObject(engine, payload);
    }

    private static object TopicToAnon(HelpTopic t) => new
    {
        id = t.Id,
        title = t.Title,
        category = t.Category,
        brief = t.Brief,
        body = t.Body,
        syntax = t.Syntax.ToArray(),
        keywords = t.Keywords.ToArray(),
        seeAlso = t.SeeAlso.ToArray()
    };

    // If first arg is a GUID -> player context, second arg is term.
    // If first arg is not a GUID -> no player, first arg is term.
    internal static (string? entityId, string? term) ResolveArgs(JsValue first, JsValue second)
    {
        if (first is null || first.Type == Types.Undefined || first.Type == Types.Null) { return (null, null); }
        if (second is null || second.Type == Types.Undefined || second.Type == Types.Null)
        {
            return (null, first.Type == Types.String ? first.ToString() : null);
        }
        if (IsGuid(first))
        {
            return (first.ToString(), second.ToString());
        }
        return (null, first.Type == Types.String ? first.ToString() : null);
    }

    private static bool IsGuid(JsValue val)
    {
        return val is not null && val.Type == Types.String && Guid.TryParse(val.ToString(), out _);
    }
}
