// tests/Tapestry.Engine.Tests/Alignment/AlignmentManagerTests.cs
using Tapestry.Engine.Alignment;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Alignment;

public class AlignmentManagerTests
{
    private (AlignmentManager mgr, World world, List<GameEvent> published) BuildManager()
    {
        var world = new World();
        var eventBus = new EventBus();
        var config = new AlignmentConfig();
        var published = new List<GameEvent>();
        eventBus.Subscribe("*", e => published.Add(e));
        var mgr = new AlignmentManager(world, eventBus, config);
        return (mgr, world, published);
    }

    private (AlignmentManager mgr, World world, EventBus bus, List<GameEvent> published) BuildManagerWithBus()
    {
        var world = new World();
        var eventBus = new EventBus();
        var config = new AlignmentConfig();
        var published = new List<GameEvent>();
        eventBus.Subscribe("*", e => published.Add(e));
        var mgr = new AlignmentManager(world, eventBus, config);
        return (mgr, world, eventBus, published);
    }

    private Entity AddEntity(World world, params string[] tags)
    {
        var e = new Entity("player", "Tester");
        foreach (var t in tags) { e.AddTag(t); }
        world.TrackEntity(e);
        return e;
    }

    [Theory]
    [InlineData(-350, "evil")]
    [InlineData(-351, "evil")]
    [InlineData(-1000, "evil")]
    [InlineData(-349, "neutral")]
    [InlineData(0, "neutral")]
    [InlineData(349, "neutral")]
    [InlineData(350, "good")]
    [InlineData(1000, "good")]
    public void Bucket_ReturnsCorrectBucketAtBoundaries(int alignment, string expectedBucket)
    {
        var (mgr, world, _) = BuildManager();
        var entity = AddEntity(world);
        mgr.Set(entity.Id, alignment, "test");
        Assert.Equal(expectedBucket, mgr.Bucket(entity.Id));
    }

    [Fact]
    public void Set_ClampsToBounds()
    {
        var (mgr, world, _) = BuildManager();
        var entity = AddEntity(world);
        mgr.Set(entity.Id, 9999, "test");
        Assert.Equal(1000, mgr.Get(entity.Id));
        mgr.Set(entity.Id, -9999, "test");
        Assert.Equal(-1000, mgr.Get(entity.Id));
    }

    [Fact]
    public void Shift_ClampsToBounds()
    {
        var (mgr, world, _) = BuildManager();
        var entity = AddEntity(world);
        mgr.Set(entity.Id, 990, "init");
        mgr.Shift(entity.Id, 100, "test");
        Assert.Equal(1000, mgr.Get(entity.Id));
    }

    [Fact]
    public void BucketTag_UpdatesOnlyWhenThresholdCrossed()
    {
        var (mgr, world, published) = BuildManager();
        var entity = AddEntity(world);
        mgr.Set(entity.Id, 0, "init");           // neutral
        published.Clear();

        mgr.Shift(entity.Id, 10, "test");   // still neutral
        Assert.DoesNotContain(published, e => e.Type == "alignment.bucket.changed");

        mgr.Shift(entity.Id, 340, "test");  // crosses to good
        Assert.Single(published, e => e.Type == "alignment.bucket.changed");
        Assert.True(entity.HasTag("alignment_good"));
        Assert.False(entity.HasTag("alignment_neutral"));
    }

    [Fact]
    public void History_EvictsOldestAtCapacity()
    {
        var (mgr, world, _) = BuildManager();
        var entity = AddEntity(world);
        for (int i = 0; i < 25; i++) { mgr.Shift(entity.Id, 1, $"shift_{i}"); }
        Assert.Equal(20, mgr.History(entity.Id).Count);
        Assert.Equal("shift_5", mgr.History(entity.Id)[0].Reason);  // oldest surviving is index 5
    }

    [Fact]
    public void Shift_OnAdminEntity_IsNoOp()
    {
        var (mgr, world, published) = BuildManager();
        var entity = AddEntity(world, "admin");
        mgr.Shift(entity.Id, 100, "test");
        Assert.Equal(0, mgr.Get(entity.Id));
        Assert.DoesNotContain(published, e => e.Type == "alignment.shifted");
    }

    [Fact]
    public void Set_OnAdminEntity_StillWorks()
    {
        var (mgr, world, _) = BuildManager();
        var entity = AddEntity(world, "admin");
        mgr.Set(entity.Id, 500, "force");
        Assert.Equal(500, mgr.Get(entity.Id));
    }

    [Fact]
    public void Shift_FiresShiftedEvent()
    {
        var (mgr, world, published) = BuildManager();
        var entity = AddEntity(world);
        mgr.Shift(entity.Id, 10, "test");
        Assert.Single(published, e => e.Type == "alignment.shifted");
    }

    [Fact]
    public void Shift_WithCancelTrue_NoChange()
    {
        var (mgr, world, eventBus, published) = BuildManagerWithBus();
        var entity = AddEntity(world);
        eventBus.Subscribe("alignment.shift.check", e => e.Data["cancel"] = true);
        mgr.Shift(entity.Id, 50, "test");
        Assert.Equal(0, mgr.Get(entity.Id));
    }

    [Fact]
    public void Shift_SubscriberCanMutateSuggestedDelta()
    {
        var (mgr, world, eventBus, _) = BuildManagerWithBus();
        var entity = AddEntity(world);
        eventBus.Subscribe("alignment.shift.check", e => e.Data["suggestedDelta"] = 3);
        mgr.Shift(entity.Id, 99, "test");
        Assert.Equal(3, mgr.Get(entity.Id));
    }
}
