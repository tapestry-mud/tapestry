using System;
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Watch;
using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class WatchModuleTests
{
    private static (WatchModule module, WatchRegistry watch, SessionManager sessions,
                    Entity admin, Entity bob, FakeConnection adminConn, FakeConnection bobConn)
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
        return (new WatchModule(watch, sessions), watch, sessions, admin, bob, adminConn, bobConn);
    }

    [Fact]
    public void Start_SubscribesAdminConnectionToTarget()
    {
        var (module, watch, _, admin, bob, adminConn, _) = Setup();
        module.Start(admin.Id.ToString(), bob.Id.ToString()).Should().BeTrue();
        // The resolver is live: GetSinks returns whatever the session's Connection is right now.
        watch.GetSinks(bob.Id).Should().ContainSingle().Which.Should().BeSameAs(adminConn);
    }

    [Fact]
    public void Stop_Unsubscribes()
    {
        var (module, watch, _, admin, bob, _, _) = Setup();
        module.Start(admin.Id.ToString(), bob.Id.ToString());
        module.Stop(admin.Id.ToString()).Should().BeTrue();
        watch.GetSinks(bob.Id).Should().BeEmpty();
    }

    [Fact]
    public void Start_UnknownTarget_ReturnsFalse()
    {
        var (module, _, _, admin, _, _, _) = Setup();
        module.Start(admin.Id.ToString(), Guid.NewGuid().ToString()).Should().BeFalse();
    }

    [Fact]
    public void Start_BadGuid_ReturnsFalse()
    {
        var (module, _, _, admin, _, _, _) = Setup();
        module.Start(admin.Id.ToString(), "not-a-guid").Should().BeFalse();
    }

    [Fact]
    public void Namespace_IsWatch()
    {
        var (module, _, _, _, _, _, _) = Setup();
        module.Namespace.Should().Be("watch");
    }

    /// <summary>
    /// Self-subscription guard: watching yourself causes infinite recursion through the tee.
    /// Start must return false and leave no subscription in the registry.
    /// </summary>
    [Fact]
    public void Start_SelfSnoop_ReturnsFalseAndNoSubscription()
    {
        var (module, watch, _, admin, _, _, _) = Setup();
        module.Start(admin.Id.ToString(), admin.Id.ToString()).Should().BeFalse();
        watch.GetSinks(admin.Id).Should().BeEmpty();
    }

    /// <summary>
    /// Reconnect-safety: after Start, swapping the watcher's Connection (simulating a
    /// link-dead reconnect via ReplaceConnection) means GetSinks resolves to the NEW
    /// connection without requiring a re-subscribe.
    /// </summary>
    [Fact]
    public void Start_WatcherReconnects_GetSinksResolvesToNewConnection()
    {
        var (module, watch, sessions, admin, bob, adminConn, _) = Setup();
        module.Start(admin.Id.ToString(), bob.Id.ToString()).Should().BeTrue();

        // Verify original connection is resolved.
        watch.GetSinks(bob.Id).Should().ContainSingle().Which.Should().BeSameAs(adminConn);

        // Simulate reconnect: admin's session gets a new Connection.
        var newAdminConn = new FakeConnection();
        sessions.GetByEntityId(admin.Id)!.ReplaceConnection(newAdminConn);

        // The resolver is live — GetSinks now returns the new connection.
        watch.GetSinks(bob.Id).Should().ContainSingle().Which.Should().BeSameAs(newAdminConn);
    }
}
