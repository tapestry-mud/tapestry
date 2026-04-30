using Tapestry.Engine;
using Tapestry.Engine.Flow;

namespace Tapestry.Engine.Tests.Flow;

public class PlayerCreatorTests
{
    [Fact]
    public void TrackAndGet_RoundTrips()
    {
        var creator = new PlayerCreator();
        var entity = new Entity("player", "TestPlayer");
        creator.TrackEntity(entity);
        Assert.Equal(entity, creator.GetEntity(entity.Id));
    }

    [Fact]
    public void GetEntity_ReturnsNull_WhenNotTracked()
    {
        var creator = new PlayerCreator();
        Assert.Null(creator.GetEntity(Guid.NewGuid()));
    }

    [Fact]
    public void Remove_RemovesEntity()
    {
        var creator = new PlayerCreator();
        var entity = new Entity("player", "TestPlayer");
        creator.TrackEntity(entity);
        creator.Remove(entity.Id);
        Assert.Null(creator.GetEntity(entity.Id));
    }

    [Fact]
    public void Contains_ReflectsState()
    {
        var creator = new PlayerCreator();
        var entity = new Entity("player", "TestPlayer");
        Assert.False(creator.Contains(entity.Id));
        creator.TrackEntity(entity);
        Assert.True(creator.Contains(entity.Id));
        creator.Remove(entity.Id);
        Assert.False(creator.Contains(entity.Id));
    }

    [Fact]
    public void All_EnumeratesTrackedEntities()
    {
        var creator = new PlayerCreator();
        var e1 = new Entity("player", "One");
        var e2 = new Entity("player", "Two");
        creator.TrackEntity(e1);
        creator.TrackEntity(e2);
        Assert.Equal(2, creator.All.Count());
    }
}
