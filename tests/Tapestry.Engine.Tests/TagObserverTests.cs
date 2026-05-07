using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class TagObserverTests
{
    private class SpyObserver : ITagObserver
    {
        public List<(Entity Entity, string Tag)> Added = new();
        public List<(Entity Entity, string Tag)> Removed = new();

        public void OnTagAdded(Entity entity, string tag) { Added.Add((entity, tag)); }
        public void OnTagRemoved(Entity entity, string tag) { Removed.Add((entity, tag)); }
    }

    [Fact]
    public void AddTag_NotifiesObserver()
    {
        var entity = new Entity("npc", "Goblin");
        var spy = new SpyObserver();
        entity.RegisterTagObserver(spy);

        entity.AddTag("npc");

        spy.Added.Should().ContainSingle(x => x.Entity == entity && x.Tag == "npc");
    }

    [Fact]
    public void AddTag_NoNotification_WhenTagAlreadyPresent()
    {
        var entity = new Entity("npc", "Goblin");
        var spy = new SpyObserver();
        entity.AddTag("npc");
        entity.RegisterTagObserver(spy);

        entity.AddTag("npc");

        spy.Added.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTag_NotifiesObserver()
    {
        var entity = new Entity("npc", "Goblin");
        var spy = new SpyObserver();
        entity.AddTag("npc");
        entity.RegisterTagObserver(spy);

        entity.RemoveTag("npc");

        spy.Removed.Should().ContainSingle(x => x.Entity == entity && x.Tag == "npc");
    }

    [Fact]
    public void RemoveTag_NoNotification_WhenTagAbsent()
    {
        var entity = new Entity("npc", "Goblin");
        var spy = new SpyObserver();
        entity.RegisterTagObserver(spy);

        entity.RemoveTag("nothere");

        spy.Removed.Should().BeEmpty();
    }

    [Fact]
    public void UnregisterTagObserver_StopsNotifications()
    {
        var entity = new Entity("npc", "Goblin");
        var spy = new SpyObserver();
        entity.RegisterTagObserver(spy);
        entity.UnregisterTagObserver(spy);

        entity.AddTag("npc");

        spy.Added.Should().BeEmpty();
    }

    [Fact]
    public void MultipleObservers_AllNotified()
    {
        var entity = new Entity("npc", "Goblin");
        var spy1 = new SpyObserver();
        var spy2 = new SpyObserver();
        entity.RegisterTagObserver(spy1);
        entity.RegisterTagObserver(spy2);

        entity.AddTag("npc");

        spy1.Added.Should().ContainSingle();
        spy2.Added.Should().ContainSingle();
    }
}
