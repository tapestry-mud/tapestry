using System;
using System.IO;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Authoring;
using Tapestry.Shared;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tapestry.Engine.Tests.Oracle;

public class StubExitTests
{
    [Fact]
    public void Move_ThroughStub_WithNoResolver_FailsGracefully()
    {
        var world = new World();
        var resolver = new StubExitResolver();
        var r1 = new Room("oracle:r1", "Start", "desc");
        var stub = new Exit("") { IsStub = true, DisplayName = "north path" };
        r1.SetExit(Direction.North, stub);
        world.AddRoom(r1);
        var player = new Entity("player", "Hero");
        r1.AddEntity(player);
        world.TrackEntity(player);

        var moved = world.MoveEntity(player, Direction.North, resolver);

        Assert.False(moved);
        Assert.Equal("oracle:r1", player.LocationRoomId);
    }

    [Fact]
    public void Move_ThroughStub_WithResolver_MintsAndCompletes()
    {
        var world = new World();
        var resolver = new StubExitResolver();
        var r1 = new Room("oracle:r1", "Start", "desc");
        r1.SetExit(Direction.North, new Exit("") { IsStub = true, DisplayName = "north path" });
        world.AddRoom(r1);
        var player = new Entity("player", "Hero");
        r1.AddEntity(player);
        world.TrackEntity(player);

        resolver.Register((roomId, dir) =>
        {
            var r2 = new Room("oracle:r2", "North", "minted");
            world.AddRoom(r2);
            world.GetRoom(roomId)!.SetExit(Direction.North, new Exit("oracle:r2"));
            return true;
        });

        var moved = world.MoveEntity(player, Direction.North, resolver);

        Assert.True(moved);
        Assert.Equal("oracle:r2", player.LocationRoomId);
    }

    private static (YamlDotNet.Serialization.ISerializer ser, YamlDotNet.Serialization.IDeserializer deser) ExitYaml()
    {
        var ser = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new Tapestry.Engine.Authoring.ExitDataConverter()).Build();
        var deser = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new Tapestry.Engine.Authoring.ExitDataConverter()).Build();
        return (ser, deser);
    }

    [Fact]
    public void RoomData_RoundTripsStubAndRealExit()
    {
        var (ser, deser) = ExitYaml();
        var data = new Tapestry.Engine.Authoring.RoomData { Id = "oracle:r1" };
        data.Exits["north"] = new Tapestry.Engine.Authoring.ExitData { Stub = true, Label = "north path" };
        data.Exits["south"] = new Tapestry.Engine.Authoring.ExitData { Target = "oracle:r0" };
        var round = deser.Deserialize<Tapestry.Engine.Authoring.RoomData>(ser.Serialize(data));
        Assert.True(round.Exits["north"].Stub);
        Assert.Equal("north path", round.Exits["north"].Label);
        Assert.Equal("oracle:r0", round.Exits["south"].Target);
        Assert.False(round.Exits["south"].Stub);
    }

    [Fact]
    public void ExitData_DeserializesLegacyScalarForm()
    {
        var (_, deser) = ExitYaml();
        var round = deser.Deserialize<Tapestry.Engine.Authoring.RoomData>("id: r\nexits:\n  down: \"core:pit\"\n");
        Assert.Equal("core:pit", round.Exits["down"].Target);
        Assert.False(round.Exits["down"].Stub);
    }

    [Fact]
    public void NonStubExit_EmitsBareScalar_ByteIdenticalToLegacy()
    {
        var (ser, _) = ExitYaml();
        var data = new Tapestry.Engine.Authoring.RoomData { Id = "r" };
        data.Exits["down"] = new Tapestry.Engine.Authoring.ExitData { Target = "core:pit" };
        var yaml = ser.Serialize(data);
        Assert.Contains("down: core:pit", yaml);
        Assert.DoesNotContain("stub:", yaml);
    }

    [Fact]
    public void StubExit_SurvivesWriteThenReload_ThroughRealLoader()
    {
        // Arrange: write a sidecar with a stub exit using the real Serializer (with ExitDataConverter),
        // then reload through the real ParseExit/LoadRoom path
        var dir = Path.Combine(Path.GetTempPath(), "e3_stub_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Build a RoomData with a stub exit and serialize using the ExitDataConverter-equipped serializer
            var ser = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(
                    YamlDotNet.Serialization.DefaultValuesHandling.OmitNull | YamlDotNet.Serialization.DefaultValuesHandling.OmitEmptyCollections)
                .WithTypeConverter(new ExitDataConverter())
                .Build();

            var data = new RoomData { Id = "oracle:stub-room", Name = "Stub Room", Description = "test" };
            data.Exits["north"] = new ExitData { Stub = true, Label = "a misty passage" };
            data.Exits["south"] = new ExitData { Target = "oracle:origin" };
            var yaml = ser.Serialize(data);
            var yamlPath = Path.Combine(dir, "stub-room.yaml");
            File.WriteAllText(yamlPath, yaml);

            // Reload through the real LoadRoom path (which uses ParseExit)
            var props = new PropertyRegistry();
            var tags = new TagRegistry();
            var result = YamlContentLoader.LoadRoom(yaml, props, tags);
            var room = result.Room;

            var northExit = room.GetExit(Direction.North);
            var southExit = room.GetExit(Direction.South);

            Assert.NotNull(northExit);
            Assert.True(northExit!.IsStub, "north exit must have IsStub=true after reload");
            Assert.Equal("a misty passage", northExit.DisplayName);
            Assert.NotNull(southExit);
            Assert.False(southExit!.IsStub, "south exit must not be a stub");
            Assert.Equal("oracle:origin", southExit.TargetRoomId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
