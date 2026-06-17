using FluentAssertions;
using System.Text.Json;
using Tapestry.Shared;
using Tapestry.Shared.Help;
using Tapestry.Engine;
using Tapestry.Engine.Help;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class CharCommandsHandlerTests
{
    private static CharCommandsHandler BuildHandler(
        out FakeGmcpConnectionManager cm,
        out CommandRegistry registry,
        HelpService? helpService = null)
    {
        cm = new FakeGmcpConnectionManager();
        registry = new CommandRegistry();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var help = helpService ?? new HelpService();
        var handler = new CharCommandsHandler(cm, sessions, world, eb, registry, help);
        handler.Configure();
        return handler;
    }

    [Fact]
    public void SendBurst_SendsCharCommandsPackage()
    {
        var help = new HelpService();
        help.AddTopic(new HelpTopic { Id = "look", Title = "look", Brief = "Look around.", Category = "movement" });
        var handler = BuildHandler(out var cm, out var registry, help);
        registry.Register("look", _ => { }, packName: "core", description: "Look around.");

        var entity = new Entity("player", "Test");
        var conn = new FakeConnection();
        handler.SendBurst(conn.Id, entity);

        cm.Sent.Should().ContainSingle(x => x.Package == "Char.Commands");
    }

    [Fact]
    public void SendBurst_CharCommands_IncludesRegisteredCommand()
    {
        var help = new HelpService();
        help.AddTopic(new HelpTopic { Id = "look", Title = "look", Brief = "Look around.", Category = "movement" });
        var handler = BuildHandler(out var cm, out var registry, help);
        registry.Register("look", _ => { }, packName: "core", description: "Look around.", category: "movement");

        var entity = new Entity("player", "Test");
        handler.SendBurst("conn1", entity);

        var sent = cm.Sent.First(x => x.Package == "Char.Commands");
        var json = JsonSerializer.Serialize(sent.Payload);
        json.Should().Contain("look");
    }

    [Fact]
    public void SendBurst_CommandWithTopicCategory_UsesCategoryFromTopic()
    {
        var help = new HelpService();
        help.AddTopic(new HelpTopic { Id = "kill", Title = "kill", Brief = "Attack.", Category = "combat" });
        var handler = BuildHandler(out var cm, out var registry, help);
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
    public void SendBurst_HiddenCommand_IsExcluded()
    {
        var help = new HelpService();
        help.AddTopic(new HelpTopic { Id = "secret", Title = "secret", Brief = "Hidden.", Category = "admin", Hidden = true });
        var handler = BuildHandler(out var cm, out var registry, help);
        registry.Register("secret", _ => { }, packName: "core", description: "Hidden.");

        var entity = new Entity("player", "Test");
        handler.SendBurst("conn1", entity);

        var sent = cm.Sent.First(x => x.Package == "Char.Commands");
        var json = JsonSerializer.Serialize(sent.Payload);
        json.Should().NotContain("secret");
    }

    [Fact]
    public void SendBurst_CommandWithNoTopic_IsExcluded()
    {
        var help = new HelpService();
        var handler = BuildHandler(out var cm, out var registry, help);
        registry.Register("notopiccommand", _ => { }, packName: "core", description: "No topic.");

        var entity = new Entity("player", "Test");
        handler.SendBurst("conn1", entity);

        var sent = cm.Sent.First(x => x.Package == "Char.Commands");
        var json = JsonSerializer.Serialize(sent.Payload);
        json.Should().NotContain("notopiccommand");
    }

    [Fact]
    public void PackageNames_ContainsCharCommands()
    {
        var handler = BuildHandler(out _, out _);
        handler.PackageNames.Should().Contain("Char.Commands");
    }
}
