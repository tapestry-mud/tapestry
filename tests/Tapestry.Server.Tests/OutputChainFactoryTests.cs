using System;
using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Color;
using Tapestry.Engine.Text;
using Tapestry.Engine.Watch;
using Tapestry.Server;
using Tapestry.Server.Tests.Fakes;
using Xunit;

namespace Tapestry.Server.Tests;

public class OutputChainFactoryTests
{
    private static ColorRenderer MakeRenderer()
    {
        var theme = new ThemeRegistry();
        theme.Compile();
        return new ColorRenderer(theme);
    }

    private static OutputWrapper MakeWrapper() => new OutputWrapper();

    [Fact]
    public void Build_Chain_TeesPlayerOutputToWatcher()
    {
        var raw = new FakeConnection("conn-1");          // the player's raw transport
        var sessions = new SessionManager();
        var entity = new Entity("player", "Bob");
        entity.AddTag("player");
        var room = new Room("room1", "room1", "A room.");
        room.AddEntity(entity);
        var session = new PlayerSession(raw, entity);
        sessions.Add(session);                           // GetByConnectionId("conn-1") -> Bob

        var watch = new WatchRegistry();
        var sink = new FakeConnection();                 // the admin watcher
        watch.Subscribe(entity.Id, "admin", () => sink);

        var chain = OutputChainFactory.Build(
            raw, MakeRenderer(), MakeWrapper(), new OutputWidthService(new ServerConfig()), sessions, watch);

        chain.SendLine("hello world");

        raw.SentLines.Should().ContainSingle();    // player still receives output
        sink.SentLines.Should().ContainSingle();   // watcher receives the mirror
    }
}
