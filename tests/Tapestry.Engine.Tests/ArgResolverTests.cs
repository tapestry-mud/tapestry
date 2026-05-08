using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class ArgResolverTests
{
    [Fact]
    public void Resolve_Keyword_ReturnsRawToken()
    {
        var world = new World();
        var actor = new ActorContext { EntityId = Guid.NewGuid(), Source = "player" };
        var argDefs = new Dictionary<string, ArgDefinition>
        {
            ["stat"] = new ArgDefinition { Type = "keyword", Required = true }
        };

        var (success, resolved, _) = ArgResolver.Resolve(actor, argDefs, ["strength"], world, null);

        Assert.True(success);
        Assert.Equal("strength", resolved["stat"]);
    }

    [Fact]
    public void Resolve_Number_ParsesInteger()
    {
        var world = new World();
        var actor = new ActorContext { EntityId = Guid.NewGuid(), Source = "player" };
        var argDefs = new Dictionary<string, ArgDefinition>
        {
            ["amount"] = new ArgDefinition { Type = "number", Required = true }
        };

        var (success, resolved, _) = ArgResolver.Resolve(actor, argDefs, ["50"], world, null);

        Assert.True(success);
        Assert.Equal(50, resolved["amount"]);
    }

    [Fact]
    public void Resolve_Number_InvalidInput_ReturnsFail()
    {
        var world = new World();
        var actor = new ActorContext { EntityId = Guid.NewGuid(), Source = "player" };
        var argDefs = new Dictionary<string, ArgDefinition>
        {
            ["amount"] = new ArgDefinition { Type = "number", Required = true }
        };

        var (success, _, _) = ArgResolver.Resolve(actor, argDefs, ["notanumber"], world, null);

        Assert.False(success);
    }

    [Fact]
    public void Resolve_Text_ConsumesAllRemainingTokens()
    {
        var world = new World();
        var actor = new ActorContext { EntityId = Guid.NewGuid(), Source = "player" };
        var argDefs = new Dictionary<string, ArgDefinition>
        {
            ["message"] = new ArgDefinition { Type = "text", Required = true }
        };

        var (success, resolved, _) = ArgResolver.Resolve(actor, argDefs, ["hello", "world", "!"], world, null);

        Assert.True(success);
        Assert.Equal("hello world !", resolved["message"]);
    }

    [Fact]
    public void Resolve_Prepositions_StrippedBeforeArg()
    {
        var world = new World();
        var actor = new ActorContext { EntityId = Guid.NewGuid(), Source = "player" };
        var argDefs = new Dictionary<string, ArgDefinition>
        {
            ["item"] = new ArgDefinition { Type = "keyword", Required = true },
            ["target"] = new ArgDefinition { Type = "keyword", Required = true, Prepositions = ["to"] }
        };

        var (success, resolved, _) = ArgResolver.Resolve(actor, argDefs, ["blade", "to", "lan"], world, null);

        Assert.True(success);
        Assert.Equal("blade", resolved["item"]);
        Assert.Equal("lan", resolved["target"]);
    }

    [Fact]
    public void Resolve_OptionalArg_MissingInput_ReturnsNullForArg()
    {
        var world = new World();
        var actor = new ActorContext { EntityId = Guid.NewGuid(), Source = "player" };
        var argDefs = new Dictionary<string, ArgDefinition>
        {
            ["target"] = new ArgDefinition { Type = "keyword", Required = false }
        };

        var (success, resolved, _) = ArgResolver.Resolve(actor, argDefs, [], world, null);

        Assert.True(success);
        Assert.Null(resolved["target"]);
    }

    [Fact]
    public void Resolve_RequiredArg_MissingInput_ReturnsFail()
    {
        var world = new World();
        var actor = new ActorContext { EntityId = Guid.NewGuid(), Source = "player" };
        var argDefs = new Dictionary<string, ArgDefinition>
        {
            ["item"] = new ArgDefinition { Type = "keyword", Required = true }
        };

        var (success, _, _) = ArgResolver.Resolve(actor, argDefs, [], world, null);

        Assert.False(success);
    }
}
