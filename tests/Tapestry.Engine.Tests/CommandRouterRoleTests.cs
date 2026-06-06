using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class CommandRouterRoleTests
{
    private static BadInputTracker MakeTracker() =>
        new BadInputTracker(Microsoft.Extensions.Logging.Abstractions.NullLogger<BadInputTracker>.Instance, null);

    [Theory]
    [InlineData("admin", true)]
    [InlineData("builder", true)]
    [InlineData("player", false)]
    public void Route_MultiRoleCommand_DispatchesForAnyDeclaredRole(string actorRole, bool expectedDispatch)
    {
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var dispatched = false;
        registry.Register("redecorate", _ => { dispatched = true; }, roles: ["admin", "builder"]);

        var router = new CommandRouter(registry, sessions, world, MakeTracker());

        var connection = new FakeConnection();
        var entity = new Entity("player", "Tester");
        entity.AddRole(actorRole);
        var session = new PlayerSession(connection, entity);
        sessions.Add(session);
        world.TrackEntity(entity);

        var ctx = MakeContext("redecorate", entity.Id);
        router.Route(ctx);

        dispatched.Should().Be(expectedDispatch);
        if (!expectedDispatch)
        {
            string.Join("", connection.SentText).Should().Contain("Huh?");
        }
    }

    [Fact]
    public void Route_PlayerMobCommand_DispatchesForPlainPlayer()
    {
        // 'mob' is an actor-type role (mobs may also invoke), NOT a privilege grant.
        // A normal player holds 'player' but not 'mob' — get/drop/say/etc. must still dispatch.
        var registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var dispatched = false;
        registry.Register("get", _ => { dispatched = true; }, roles: ["player", "mob"]);

        var router = new CommandRouter(registry, sessions, world, MakeTracker());

        var connection = new FakeConnection();
        var entity = new Entity("player", "Tester");
        entity.AddRole("player");
        var session = new PlayerSession(connection, entity);
        sessions.Add(session);
        world.TrackEntity(entity);

        var ctx = MakeContext("get sword", entity.Id);
        router.Route(ctx);

        dispatched.Should().BeTrue();
        string.Join("", connection.SentText).Should().NotContain("Huh?");
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
