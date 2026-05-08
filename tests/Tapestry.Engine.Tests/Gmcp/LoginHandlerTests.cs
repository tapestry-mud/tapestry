using FluentAssertions;
using System.Text.Json;
using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class LoginHandlerTests
{
    private static LoginHandler BuildHandler(out FakeGmcpConnectionManager cm, out PostLoginOrchestrator orchestrator)
    {
        cm = new FakeGmcpConnectionManager();
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        orchestrator = new PostLoginOrchestrator(Array.Empty<IGmcpPackageHandler>(), Array.Empty<Type>());
        var handler = new LoginHandler(cm, sessions, world, eb, orchestrator);
        handler.Configure();
        return handler;
    }

    [Fact]
    public void SendLoginPhase_SendsCharLoginPhasePackage()
    {
        var handler = BuildHandler(out var cm, out _);

        handler.SendLoginPhase("conn1", "name");

        cm.Sent.Should().ContainSingle(x => x.Package == "Char.Login.Phase");
    }

    [Fact]
    public void SendLoginPhase_PayloadContainsPhase()
    {
        var handler = BuildHandler(out var cm, out _);

        handler.SendLoginPhase("conn1", "playing");

        var sent = cm.Sent.First(x => x.Package == "Char.Login.Phase");
        var json = JsonSerializer.Serialize(sent.Payload);
        JsonDocument.Parse(json).RootElement.GetProperty("phase").GetString().Should().Be("playing");
    }

    [Fact]
    public void SendLoginPrompt_SendsLoginPromptPackage()
    {
        var handler = BuildHandler(out var cm, out _);

        handler.SendLoginPrompt("conn1", "Enter your name");

        cm.Sent.Should().ContainSingle(x => x.Package == "Login.Prompt");
    }

    [Fact]
    public void SendBurst_IsNoOp()
    {
        var handler = BuildHandler(out var cm, out _);

        handler.SendBurst("conn1", new Entity("player", "Test"));

        cm.Sent.Should().BeEmpty();
    }

    [Fact]
    public void PackageNames_ContainsLoginPackages()
    {
        var handler = BuildHandler(out _, out _);
        handler.PackageNames.Should().Contain("Char.Login.Phase").And.Contain("Login.Prompt");
    }
}
