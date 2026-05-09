using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class CommandRouterTests
{
    [Fact]
    public void Route_ParsesCommandAndArgs()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        string? receivedCmd = null;
        string[]? receivedArgs = null;
        registry.Register("say", (ctx) =>
        {
            receivedCmd = ctx.Command;
            receivedArgs = ctx.RawArgs;
        }, packName: "core");
        var router = new CommandRouter(registry, sessions, world);
        var ctx = MakeContext("say hello world");
        router.Route(ctx);
        receivedCmd.Should().Be("say");
        receivedArgs.Should().BeEquivalentTo(["hello", "world"]);
    }

    [Fact]
    public void Route_UnknownCommand_SendsHuh()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world);

        var connection = new FakeConnection();
        var entity = new Entity("player", "Test");
        var session = new PlayerSession(connection, entity);
        sessions.Add(session);

        var ctx = MakeContext("xyzzy", entity.Id);
        router.Route(ctx);
        string.Join("", connection.SentText).Should().Contain("Huh?");
    }

    [Fact]
    public void Route_EmptyInput_DoesNothing()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world);
        var called = false;
        registry.Register("test", (_) => { called = true; }, packName: "core");
        var ctx = MakeContext("");
        router.Route(ctx);
        called.Should().BeFalse();
    }

    [Fact]
    public void Resolve_FindsRegisteredCommand()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        registry.Register("say", (ctx) => { }, packName: "core");
        var router = new CommandRouter(registry, sessions, world);

        var result = router.Resolve("say");

        result.Should().NotBeNull();
        result!.Keyword.Should().Be("say");
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNotRegistered()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var router = new CommandRouter(registry, sessions, world);

        var result = router.Resolve("xyzzy");

        result.Should().BeNull();
    }

    [Fact]
    public void RouteForMob_DispatchesMobRoleCommand()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var dispatched = false;
        registry.Register("say", actor => { dispatched = true; }, roles: ["player", "mob"]);

        var router = new CommandRouter(registry, sessions, world);

        router.RouteForMob(Guid.NewGuid(), "say hello", null, "Goblin");

        dispatched.Should().BeTrue();
    }

    [Fact]
    public void RouteForMob_PlayerOnlyCommand_DoesNotDispatch()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var dispatched = false;
        registry.Register("score", _ => { dispatched = true; }, roles: ["player"]);

        var router = new CommandRouter(registry, sessions, world);

        router.RouteForMob(Guid.NewGuid(), "score", null, "Goblin");

        dispatched.Should().BeFalse();
    }

    private static CommandContext MakeContext(string input, Guid? entityId = null)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new CommandContext
        {
            PlayerEntityId = entityId ?? Guid.NewGuid(),
            RawInput = input,
            Command = parts.Length > 0 ? parts[0] : "",
            Args = parts.Length > 1 ? parts[1..] : []
        };
    }
}
