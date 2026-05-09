using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class ArgResolverTests
{
    private static ArgResolver MakeResolver()
    {
        var world = new World();
        var visibility = new VisibilityFilter();
        var eventBus = new EventBus();
        var doors = new DoorService(world, eventBus);
        var logger = NullLogger<ArgResolver>.Instance;
        return new ArgResolver(world, visibility, doors, logger);
    }

    private static ActorContext MakeActor(Guid entityId, string? roomId = null)
    {
        return new ActorContext
        {
            EntityId = entityId,
            RoomId = roomId
        };
    }

    // ── MatchesInput ──────────────────────────────────────────────────────

    [Fact]
    public void MatchesInput_ByKeyword_ReturnsTrue()
    {
        var entity = new Entity("npc", "A green goblin");
        entity.AddKeyword("goblin");

        ArgResolver.MatchesInput(entity, "goblin").Should().BeTrue();
    }

    [Fact]
    public void MatchesInput_ByNameSubstring_ReturnsTrue()
    {
        var entity = new Entity("item", "A large iron shield");

        ArgResolver.MatchesInput(entity, "large").Should().BeTrue();
    }

    [Fact]
    public void MatchesInput_NoMatch_ReturnsFalse()
    {
        var entity = new Entity("npc", "A green goblin");
        entity.AddKeyword("goblin");

        ArgResolver.MatchesInput(entity, "dragon").Should().BeFalse();
    }

    // ── ResolveToken: number ──────────────────────────────────────────────

    [Fact]
    public void ResolveNumber_ValidInt_ReturnsSuccess()
    {
        var resolver = MakeResolver();
        var actor = MakeActor(Guid.NewGuid());
        var def = new ArgDefinition { Type = "number", Required = true };

        var (success, value, error) = resolver.ResolveToken(actor, "amount", def, "42");

        success.Should().BeTrue();
        value.Should().Be(42);
        error.Should().BeNull();
    }

    [Fact]
    public void ResolveNumber_Invalid_ReturnsFail()
    {
        var resolver = MakeResolver();
        var actor = MakeActor(Guid.NewGuid());
        var def = new ArgDefinition { Type = "number", Required = true };

        var (success, value, error) = resolver.ResolveToken(actor, "amount", def, "goblin");

        success.Should().BeFalse();
        value.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    // ── RegisterPackType ──────────────────────────────────────────────────

    [Fact]
    public void RegisterPackType_EngineType_IsIgnored()
    {
        var resolver = MakeResolver();
        var actor = MakeActor(Guid.NewGuid());
        var def = new ArgDefinition { Type = "keyword", Required = true };

        // Attempt to override the engine "keyword" type with a handler that always fails
        resolver.RegisterPackType("keyword", (_, _, _) => (false, null, "overridden"));

        // The engine handler should still win -- keyword passthrough returns success
        var (success, value, error) = resolver.ResolveToken(actor, "word", def, "sword");

        success.Should().BeTrue();
        value.Should().Be("sword");
    }

    [Fact]
    public void RegisterPackType_Custom_IsInvoked()
    {
        var resolver = MakeResolver();
        var actor = MakeActor(Guid.NewGuid());
        var def = new ArgDefinition { Type = "recipe", Required = true };

        resolver.RegisterPackType("recipe", (_, _, token) => (true, $"recipe:{token}", null));

        var (success, value, error) = resolver.ResolveToken(actor, "item", def, "bread");

        success.Should().BeTrue();
        value.Should().Be("recipe:bread");
    }

    // ── ResolveToken: unknown type fallback ───────────────────────────────

    [Fact]
    public void ResolveToken_UnknownType_FallsBackToKeyword()
    {
        var resolver = MakeResolver();
        var actor = MakeActor(Guid.NewGuid());
        var def = new ArgDefinition { Type = "totally_unknown_type", Required = false };

        var (success, value, error) = resolver.ResolveToken(actor, "thing", def, "widget");

        success.Should().BeTrue();
        value.Should().Be("widget");
        error.Should().BeNull();
    }
}
