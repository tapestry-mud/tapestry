using System.Text.Json.Nodes;

namespace Tapestry.Engine.Recommend;

/// <summary>Dev/test-only: generate a minimal VALID JSON instance from a (subset) JSON Schema,
/// so the structured-output recommend path runs locally with no real LLM (the UseStub seam).
/// Not a general schema engine - handles object/array/string/number/integer/boolean + enum.</summary>
public static class StubJson
{
    private static readonly string[] Words =
    {
        "Gloom Stalker", "Cave Wretch", "Bone Gnawer", "Shadowmaw",
        "Deep Lurker", "Pale Crawler", "Rift Hound", "Mire Shade",
    };

    public static string FromSchema(string schemaJson)
    {
        JsonNode? schema;
        try
        {
            schema = JsonNode.Parse(schemaJson);
        }
        catch
        {
            return "{}";
        }
        var counter = new int[1];
        var node = Gen(schema, null, counter);
        return node?.ToJsonString() ?? "{}";
    }

    private static JsonNode? Gen(JsonNode? schema, string? prop, int[] counter)
    {
        if (schema is not JsonObject obj)
        {
            return null;
        }
        if (obj["enum"] is JsonArray en && en.Count > 0)
        {
            return en[0]?.DeepClone();
        }
        var type = obj["type"]?.GetValue<string>() ?? "object";
        switch (type)
        {
            case "object":
            {
                var result = new JsonObject();
                if (obj["properties"] is JsonObject props)
                {
                    foreach (var kv in props)
                    {
                        result[kv.Key] = Gen(kv.Value, kv.Key, counter);
                    }
                }
                return result;
            }
            case "array":
            {
                var arr = new JsonArray();
                for (var i = 0; i < 2; i++)
                {
                    arr.Add(Gen(obj["items"], prop, counter));
                }
                return arr;
            }
            case "string":
            {
                var n = (prop ?? "").ToLowerInvariant();
                if (n.Contains("name") || n.Contains("title") || n.Contains("place"))
                {
                    var w = Words[counter[0] % Words.Length];
                    counter[0]++;
                    return JsonValue.Create(w);
                }
                if (n.Contains("desc") || n.Contains("text") || n.Contains("line"))
                {
                    return JsonValue.Create("A figure looms in the gloom.");
                }
                return JsonValue.Create("stub");
            }
            case "number":
            case "integer":
                return JsonValue.Create(1);
            case "boolean":
                return JsonValue.Create(false);
            default:
                return null;
        }
    }
}
