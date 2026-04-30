using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class CommandRegistryTests
{
    [Fact]
    public void Register_AndResolve()
    {
        var registry = new CommandRegistry();
        Action<CommandContext> handler = (_) => { };
        registry.Register("look", handler, aliases: ["l"], priority: 0, packName: "core");
        registry.Resolve("look").Should().NotBeNull();
        registry.Resolve("l").Should().NotBeNull();
        registry.Resolve("look")!.Handler.Should().Be(handler);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNotRegistered()
    {
        var registry = new CommandRegistry();
        registry.Resolve("nonexistent").Should().BeNull();
    }

    [Fact]
    public void HigherPriority_WinsConflict()
    {
        var registry = new CommandRegistry();
        Action<CommandContext> lowHandler = (_) => { };
        Action<CommandContext> highHandler = (_) => { };
        registry.Register("look", lowHandler, priority: 10, packName: "base");
        registry.Register("look", highHandler, priority: 100, packName: "override");
        registry.Resolve("look")!.Handler.Should().Be(highHandler);
    }

    [Fact]
    public void CaseInsensitive()
    {
        var registry = new CommandRegistry();
        Action<CommandContext> handler = (_) => { };
        registry.Register("Look", handler, packName: "core");
        registry.Resolve("look").Should().NotBeNull();
        registry.Resolve("LOOK").Should().NotBeNull();
    }

    [Fact]
    public void PrefixMatch_SingleCharacter()
    {
        var registry = new CommandRegistry();
        Action<CommandContext> handler = (_) => { };
        registry.Register("north", handler, packName: "core");

        registry.Resolve("n").Should().NotBeNull();
        registry.Resolve("n")!.Keyword.Should().Be("north");
        registry.Resolve("no").Should().NotBeNull();
        registry.Resolve("nor").Should().NotBeNull();
        registry.Resolve("nort").Should().NotBeNull();
    }

    [Fact]
    public void PrefixMatch_ExactMatchWinsOverPrefix()
    {
        var registry = new CommandRegistry();
        Action<CommandContext> nHandler = (_) => { };
        Action<CommandContext> northHandler = (_) => { };
        // Register "n" as an explicit command AND "north"
        registry.Register("n", nHandler, packName: "core");
        registry.Register("north", northHandler, packName: "core");

        // Exact match "n" should win over prefix match to "north"
        registry.Resolve("n")!.Handler.Should().Be(nHandler);
        // "no" should prefix match to "north"
        registry.Resolve("no")!.Handler.Should().Be(northHandler);
    }

    [Fact]
    public void PrefixMatch_AmbiguousPrefix_HighestPriorityWins()
    {
        var registry = new CommandRegistry();
        Action<CommandContext> southHandler = (_) => { };
        Action<CommandContext> sayHandler = (_) => { };
        Action<CommandContext> scoreHandler = (_) => { };
        // south registered at priority 0 (movement), say at 0, score at 0
        // When all same priority, first registered wins
        registry.Register("south", southHandler, priority: 0, packName: "core");
        registry.Register("say", sayHandler, priority: 0, packName: "core");
        registry.Register("score", scoreHandler, priority: 0, packName: "core");

        // "s" is ambiguous — south was registered first at same priority
        registry.Resolve("s")!.Keyword.Should().Be("south");
        // "sa" is unambiguous — only "say" starts with "sa"
        registry.Resolve("sa")!.Handler.Should().Be(sayHandler);
        // "sc" is unambiguous — only "score"
        registry.Resolve("sc")!.Handler.Should().Be(scoreHandler);
        // "so" is unambiguous — only "south"
        registry.Resolve("so")!.Handler.Should().Be(southHandler);
    }

    [Fact]
    public void PrefixMatch_NoMatch_ReturnsNull()
    {
        var registry = new CommandRegistry();
        registry.Register("north", (_) => { }, packName: "core");

        registry.Resolve("x").Should().BeNull();
        registry.Resolve("nz").Should().BeNull();
    }

    [Fact]
    public void PrefixMatch_MatchesKeywordsNotAliases()
    {
        var registry = new CommandRegistry();
        Action<CommandContext> lookHandler = (_) => { };
        // "l" is an explicit alias for look
        registry.Register("look", lookHandler, aliases: ["l"], packName: "core");

        // "lo" should prefix-match "look"
        registry.Resolve("lo")!.Handler.Should().Be(lookHandler);
        // "l" should exact-match the alias
        registry.Resolve("l")!.Handler.Should().Be(lookHandler);
    }
}
