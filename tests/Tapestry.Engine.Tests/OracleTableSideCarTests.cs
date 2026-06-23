using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Connections;
using Tapestry.Scripting.Modules;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

/// <summary>Thin harness that wires WorldAuthoringModule against a temp root
/// and exposes the OracleTableRegistry for same-session visibility assertions.</summary>
internal sealed class TempAuthoringRoot : IDisposable
{
    private readonly string _root;
    private readonly WorldAuthoringModule _module;
    private readonly AreaRegistry _areaRegistry;

    public string RootPath => _root;
    public OracleTableRegistry Registry { get; }

    public TempAuthoringRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "tap-oracle-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_root);

        var world = new World();
        var props = new PropertyRegistry();
        var tags = new TagRegistry();
        _areaRegistry = new AreaRegistry();
        var projector = new RoomProjector(world, props, tags, _areaRegistry);
        var writer = new AttributeWriter(props, tags);
        var loadedPackNamespaces = new HashSet<string>();
        var connections = new ConnectionLoader(
            world, NullLogger<ConnectionLoader>.Instance, Path.Combine(_root, "connections"));
        Registry = new OracleTableRegistry();

        _module = new WorldAuthoringModule(
            world, projector, writer, _root, loadedPackNamespaces,
            _areaRegistry, new StubExitResolver(), oracleRegistry: Registry,
            recommend: null, connections: connections);
    }

    /// <summary>Calls the internal WriteOracleTableSideCar and returns the path written.</summary>
    public string WriteOracleTable(string areaId, OracleTable table)
    {
        return _module.WriteOracleTableSideCar(areaId, table);
    }

    public void WriteRawFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(_root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var dir = System.IO.Path.GetDirectoryName(fullPath)!;
        if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
        File.WriteAllText(fullPath, content);
    }

    public bool CreateArea(string areaId, string name) { return _module.CreateArea(areaId, name); }

    public string SetAreaAttribute(string areaId, string attr, string value)
    {
        return _module.SetAreaAttribute(areaId, attr, value);
    }

    public AreaDefinition? GetAreaDefinition(string areaId) { return _areaRegistry.Get(areaId); }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

public class OracleTableSideCarTests
{
    [Fact]
    public void WriteOracleTableSideCar_round_trips_through_the_loader()
    {
        using var root = new TempAuthoringRoot();
        var table = new OracleTable
        {
            Kind = "mobs",
            Entries = new()
            {
                new OracleEntry { W = 60, Id = "angry-cook", Name = "Angry Cook", BalanceRef = "mob" },
            },
        };

        var path = root.WriteOracleTable("castle-kitchen", table);

        Assert.True(File.Exists(path));
        var reloaded = YamlContentLoader.LoadOracleTable(File.ReadAllText(path));
        Assert.Equal("mobs", reloaded.Kind);
        Assert.Equal("angry-cook", reloaded.Entries[0].Id);
        Assert.Equal(60, reloaded.Entries[0].W);
    }

    [Fact]
    public void WriteOracleTableSideCar_puts_places_at_area_root_no_doubled_areas_segment()
    {
        using var root = new TempAuthoringRoot();
        var table = new OracleTable { Kind = "places", Entries = new() };
        var path = root.WriteOracleTable("castle-kitchen", table).Replace('\\', '/');
        Assert.EndsWith("places-oracle.yaml", path);
        // Lives in the SAME area folder as area.yaml/rooms - not a doubled areas/areas tree.
        Assert.DoesNotContain("/areas/areas/", path);
        Assert.Contains("/castle-kitchen/", path);
    }

    [Fact]
    public void WriteOracleTableSideCar_registers_into_live_registry_same_session()
    {
        using var root = new TempAuthoringRoot();
        var table = new OracleTable
        {
            Kind = "mobs",
            Entries = new() { new OracleEntry { W = 60, Id = "angry-cook", BalanceRef = "mob" } },
        };

        root.WriteOracleTable("castle-kitchen", table);

        // No reboot: the just-written table is live in the registry under the canonical id.
        var got = root.Registry.Get("castle-kitchen:mobs");
        Assert.NotNull(got);
        Assert.Equal("angry-cook", got!.Entries[0].Id);
    }
}
