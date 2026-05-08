using FluentAssertions;
using System.Text.Json;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CharCommandsHandlerTests
{
    private static CharCommandsHandler BuildHandler(out FakeGmcpConnectionManager cm, out CommandRegistry registry)
    {
        cm = new FakeGmcpConnectionManager();
        registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var handler = new CharCommandsHandler(cm, sessions, world, eb, registry);
        handler.Configure();
        return handler;
    }

    [Fact]
    public void SendBurst_SendsCharCommandsPackage()
    {
        var handler = BuildHandler(out var cm, out var registry);
        registry.Register("look", _ => { }, packName: "core", description: "Look around.");

        var entity = new Entity("player", "Test");
        var conn = new FakeConnection();
        handler.SendBurst(conn.Id, entity);

        cm.Sent.Should().ContainSingle(x => x.Package == "Char.Commands");
    }

    [Fact]
    public void SendBurst_CharCommands_IncludesRegisteredCommand()
    {
        var handler = BuildHandler(out var cm, out var registry);
        registry.Register("look", _ => { }, packName: "core", description: "Look around.", category: "movement");

        var entity = new Entity("player", "Test");
        handler.SendBurst("conn1", entity);

        var sent = cm.Sent.First(x => x.Package == "Char.Commands");
        var json = JsonSerializer.Serialize(sent.Payload);
        json.Should().Contain("look");
    }

    [Fact]
    public void SendBurst_KillCommand_GetsOverrideCategoryCombat()
    {
        var handler = BuildHandler(out var cm, out var registry);
        registry.Register("kill", _ => { }, packName: "core", description: "Attack.", category: "commands");

        var entity = new Entity("player", "Test");
        handler.SendBurst("conn1", entity);

        var sent = cm.Sent.First(x => x.Package == "Char.Commands");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        var commands = doc.RootElement.GetProperty("commands");
        var kill = commands.EnumerateArray().FirstOrDefault(c => c.GetProperty("keyword").GetString() == "kill");
        kill.GetProperty("category").GetString().Should().Be("combat");
    }

    [Fact]
    public void PackageNames_ContainsCharCommands()
    {
        var handler = BuildHandler(out _, out _);
        handler.PackageNames.Should().Contain("Char.Commands");
    }
}
