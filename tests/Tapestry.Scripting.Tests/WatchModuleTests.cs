using System;
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Watch;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class WatchModuleTests
{
    private static (WatchModule module, WatchRegistry watch, Entity admin, Entity bob, FakeConnection adminConn)
        Setup()
    {
        var sessions = new SessionManager();
        var watch = new WatchRegistry();
        var adminConn = new FakeConnection();
        var admin = new Entity("player", "Admin");
        sessions.Add(new PlayerSession(adminConn, admin));
        var bobConn = new FakeConnection();
        var bob = new Entity("player", "Bob");
        sessions.Add(new PlayerSession(bobConn, bob));
        return (new WatchModule(watch, sessions), watch, admin, bob, adminConn);
    }

    [Fact]
    public void Start_SubscribesAdminConnectionToTarget()
    {
        var (module, watch, admin, bob, adminConn) = Setup();
        module.Start(admin.Id.ToString(), bob.Id.ToString()).Should().BeTrue();
        watch.GetSinks(bob.Id).Should().ContainSingle().Which.Should().BeSameAs(adminConn);
    }

    [Fact]
    public void Stop_Unsubscribes()
    {
        var (module, watch, admin, bob, _) = Setup();
        module.Start(admin.Id.ToString(), bob.Id.ToString());
        module.Stop(admin.Id.ToString()).Should().BeTrue();
        watch.GetSinks(bob.Id).Should().BeEmpty();
    }

    [Fact]
    public void Start_UnknownTarget_ReturnsFalse()
    {
        var (module, _, admin, _, _) = Setup();
        module.Start(admin.Id.ToString(), Guid.NewGuid().ToString()).Should().BeFalse();
    }

    [Fact]
    public void Start_BadGuid_ReturnsFalse()
    {
        var (module, _, admin, _, _) = Setup();
        module.Start(admin.Id.ToString(), "not-a-guid").Should().BeFalse();
    }

    [Fact]
    public void Namespace_IsWatch()
    {
        var (module, _, _, _, _) = Setup();
        module.Namespace.Should().Be("watch");
    }
}
