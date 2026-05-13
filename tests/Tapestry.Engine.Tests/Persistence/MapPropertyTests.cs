using Tapestry.Engine;
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Tests.Persistence;

public class MapPropertyTests
{
    private readonly Entity _entity = new("player", "TestPlayer");

    [Fact]
    public void SetMapValue_ThenGetMapValue_ReturnsValue()
    {
        _entity.SetMapValue("proficiency", "kick", 100);
        Assert.Equal(100, _entity.GetMapValue<int>("proficiency", "kick"));
    }

    [Fact]
    public void GetMapValue_MissingKey_ReturnsDefault()
    {
        _entity.SetMapValue("proficiency", "kick", 100);
        Assert.Equal(0, _entity.GetMapValue<int>("proficiency", "dodge"));
    }

    [Fact]
    public void GetMapValue_MissingProperty_ReturnsDefault()
    {
        Assert.Equal(0, _entity.GetMapValue<int>("proficiency", "kick"));
    }

    [Fact]
    public void GetMap_ReturnsAllKeys()
    {
        _entity.SetMapValue("proficiency", "kick", 100);
        _entity.SetMapValue("proficiency", "dodge", 45);
        var map = _entity.GetMap<int>("proficiency");
        Assert.NotNull(map);
        Assert.Equal(2, map!.Count);
        Assert.Equal(100, map["kick"]);
        Assert.Equal(45, map["dodge"]);
    }

    [Fact]
    public void GetMap_MissingProperty_ReturnsNull()
    {
        Assert.Null(_entity.GetMap<int>("nonexistent_map"));
    }

    [Fact]
    public void RemoveMapKey_RemovesKey()
    {
        _entity.SetMapValue("proficiency", "kick", 100);
        _entity.RemoveMapKey("proficiency", "kick");
        Assert.Equal(0, _entity.GetMapValue<int>("proficiency", "kick"));
    }

    [Fact]
    public void RemoveMapKey_LastKey_PropertyStillExists()
    {
        _entity.SetMapValue("proficiency", "kick", 100);
        _entity.RemoveMapKey("proficiency", "kick");
        Assert.NotNull(_entity.GetMap<int>("proficiency"));
    }

    [Fact]
    public void SetMapValue_StringMap_Works()
    {
        _entity.SetMapValue("labels", "color", "red");
        Assert.Equal("red", _entity.GetMapValue<string>("labels", "color"));
    }

    [Fact]
    public void SetMapValue_OverwritesExistingKey()
    {
        _entity.SetMapValue("proficiency", "kick", 50);
        _entity.SetMapValue("proficiency", "kick", 75);
        Assert.Equal(75, _entity.GetMapValue<int>("proficiency", "kick"));
    }

    [Fact]
    public void SetProperty_DoesNotConflictWithMapProperty()
    {
        _entity.SetProperty("gold", 100);
        _entity.SetMapValue("proficiency", "kick", 50);
        Assert.Equal(100, _entity.GetProperty<int>("gold"));
        Assert.Equal(50, _entity.GetMapValue<int>("proficiency", "kick"));
    }

    [Fact]
    public void MapProperty_RoundTrip_ThroughSerializer()
    {
        var registry = new PropertyRegistry();
        registry.RegisterEngineProperty("proficiency", "Proficiency", PropertyValueType.MapInt);
        registry.RegisterEngineProperty("labels", "Labels", PropertyValueType.MapString);

        var entity = new Entity("player", "TestPlayer");
        entity.SetMapValue("proficiency", "kick", 100);
        entity.SetMapValue("proficiency", "dodge", 45);
        entity.SetMapValue("labels", "color", "red");

        var serializer = new PlayerSerializer(registry);
        var saveData = serializer.ToSaveData(entity, "hash", new List<Entity>());
        var loadResult = serializer.FromSaveData(saveData);
        var result = loadResult.Entity;

        Assert.Equal(100, result.GetMapValue<int>("proficiency", "kick"));
        Assert.Equal(45, result.GetMapValue<int>("proficiency", "dodge"));
        Assert.Equal("red", result.GetMapValue<string>("labels", "color"));
    }
}
