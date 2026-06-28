using System.Text.Json;
using Tapestry.Engine.Recommend;
using Xunit;

namespace Tapestry.Engine.Tests;

public class StubJsonTests
{
    [Fact]
    public void Generates_object_wrapping_an_array_of_records()
    {
        var schema = "{\"type\":\"object\",\"properties\":{\"mobs\":{\"type\":\"array\",\"items\":" +
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"},\"desc\":{\"type\":\"string\"}}," +
            "\"required\":[\"name\",\"desc\"],\"additionalProperties\":false}}},\"required\":[\"mobs\"],\"additionalProperties\":false}";

        var json = StubJson.FromSchema(schema);

        using var doc = JsonDocument.Parse(json); // valid JSON
        var mobs = doc.RootElement.GetProperty("mobs");
        Assert.True(mobs.GetArrayLength() >= 1);
        var first = mobs[0];
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("name").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("desc").GetString()));
    }

    [Fact]
    public void Honors_enums()
    {
        var schema = "{\"type\":\"object\",\"properties\":{\"r\":{\"type\":\"string\",\"enum\":[\"common\",\"rare\"]}}," +
            "\"required\":[\"r\"],\"additionalProperties\":false}";
        var json = StubJson.FromSchema(schema);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("common", doc.RootElement.GetProperty("r").GetString());
    }

    [Fact]
    public void Bad_schema_returns_empty_object()
    {
        Assert.Equal("{}", StubJson.FromSchema("not json"));
    }
}
