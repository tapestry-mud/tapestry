using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class FindPlayerByNameTests
{
    private (JintRuntime rt, World world, SessionManager sessions) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<SessionManager>());
    }

    private PlayerSession AddOnlinePlayer(SessionManager sessions, World world, string name)
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", name);
        world.TrackEntity(entity);
        var session = new PlayerSession(conn, entity);
        sessions.Add(session);
        return session;
    }

    [Fact]
    public void FindPlayerByName_ExactMatch_ReturnsPlayer()
    {
        var (rt, world, sessions) = BuildRuntime();
        AddOnlinePlayer(sessions, world, "Mallek");

        var result = rt.Evaluate("tapestry.world.findPlayerByName('mallek')");

        result.Should().NotBeNull();
    }

    [Fact]
    public void FindPlayerByName_PrefixMatch_ReturnsPlayer()
    {
        var (rt, world, sessions) = BuildRuntime();
        AddOnlinePlayer(sessions, world, "Mallek");

        var result = rt.Evaluate("tapestry.world.findPlayerByName('mall')");

        result.Should().NotBeNull();
    }

    [Fact]
    public void FindPlayerByName_NoMatch_ReturnsNull()
    {
        var (rt, world, sessions) = BuildRuntime();
        AddOnlinePlayer(sessions, world, "Mallek");

        var result = rt.Evaluate("tapestry.world.findPlayerByName('nobody')");

        result.Should().BeNull();
    }

    [Fact]
    public void FindPlayerByName_ResultHasIdAndName()
    {
        var (rt, world, sessions) = BuildRuntime();
        AddOnlinePlayer(sessions, world, "Mallek");

        var id = rt.Evaluate("tapestry.world.findPlayerByName('mallek').id");
        var name = rt.Evaluate("tapestry.world.findPlayerByName('mallek').name");

        id.Should().NotBeNull();
        name.Should().Be("Mallek");
    }
}
