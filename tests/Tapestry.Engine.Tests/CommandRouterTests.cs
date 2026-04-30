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
        string? receivedCmd = null;
        string[]? receivedArgs = null;
        registry.Register("say", (ctx) =>
        {
            receivedCmd = ctx.Command;
            receivedArgs = ctx.Args;
        }, packName: "core");
        var router = new CommandRouter(registry, sessions);
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
        var router = new CommandRouter(registry, sessions);

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
        var router = new CommandRouter(registry, sessions);
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
        registry.Register("say", (ctx) => { }, packName: "core");
        var router = new CommandRouter(registry, sessions);

        var result = router.Resolve("say");

        result.Should().NotBeNull();
        result!.Keyword.Should().Be("say");
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNotRegistered()
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var router = new CommandRouter(registry, sessions);

        var result = router.Resolve("xyzzy");

        result.Should().BeNull();
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
