using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Scripting;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

/// <summary>
/// Verifies the full save/reload round-trip for pack-declared list_string player properties.
/// Prior to the fix, known_recipes was serialized with a raw CLR type tag (type: List`1)
/// because PlayerSerializer resolved properties by bare key, missing pack properties stored
/// under "{pack}:{name}". After reload the recipe book appeared empty.
/// </summary>
public class PackPropertyPersistenceRoundTripTests
{
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .WithQuotingNecessaryStrings()
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private (JintRuntime rt, World world, PropertyRegistry props, PlayerSerializer serializer) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();

        var props = provider.GetRequiredService<PropertyRegistry>();
        // Register known_recipes as a PACK list_string property (mirrors tinkers/properties.yml).
        props.RegisterPackProperty(
            "tapestry-tinkers",
            "known_recipes",
            "The player's recipe book",
            PropertyValueType.ListString,
            appliesTo: new[] { "player" });

        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();

        var serializer = new PlayerSerializer(props);

        return (rt, provider.GetRequiredService<World>(), props, serializer);
    }

    [Fact]
    public void PackListStringProperty_SurvivesFullYamlRoundTrip_AndReloadsAsJsArray()
    {
        var (rt, world, _, serializer) = BuildRuntime();

        // --- Step 1: create a player and set known_recipes from JS (as the learn command does) ---
        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        var id = player.Id;

        rt.Execute($@"
            var raw = tapestry.world.getProperty('{id}', 'known_recipes') || [];
            var known = Array.isArray(raw) ? raw.slice() : [];
            known.push('tapestry-tinkers:level-1-bench');
            known.push('tapestry-tinkers:iron-pickaxe');
            tapestry.world.setProperty('{id}', 'known_recipes', known);
        ");

        // --- Step 2: serialize via PlayerSerializer (same path as the real save) ---
        var saveData = serializer.ToSaveData(player, Guid.NewGuid(), new List<Entity>());

        // The entity stores the property under the bare key used by JS setProperty ("known_recipes"),
        // not the fully-qualified key. TryResolveByName finds it via Name scan.
        Assert.True(
            saveData.Properties.ContainsKey("known_recipes"),
            "Expected 'known_recipes' key in saved properties");

        var rawPropValue = saveData.Properties["known_recipes"];
        Assert.False(
            rawPropValue is Dictionary<string, object?> taggedDict
                && taggedDict.TryGetValue("type", out var typeTag)
                && typeTag?.ToString()?.Contains('`') == true,
            $"Property was serialized with a raw CLR type tag (pre-fix bug). Value was: {rawPropValue?.GetType().Name}");

        // The raw saved value should be a list, not a tagged dict.
        Assert.IsAssignableFrom<System.Collections.IEnumerable>(rawPropValue);

        // --- Step 3: full YAML round-trip (serialize to string, deserialize back) ---
        // This mirrors FilePlayerStore.SaveAsync / LoadAsync and triggers the
        // Dictionary<object, object> normalization that the real load path hits.
        var yaml = YamlSerializer.Serialize(saveData);
        var reloadedSaveData = YamlDeserializer.Deserialize<PlayerSaveData>(yaml);
        Assert.NotNull(reloadedSaveData);

        // --- Step 4: reconstruct entity from reloaded save data ---
        var loadResult = serializer.FromSaveData(reloadedSaveData);
        var reloadedPlayer = loadResult.Entity;

        // Track in world so getProperty can find it.
        world.TrackEntity(reloadedPlayer);
        var reloadedId = reloadedPlayer.Id;

        // --- Step 5: read from JS and assert Array.isArray + contents ---
        // The key in the entity is the bare "known_recipes" (same as what JS setProperty used).
        var isArray = rt.Evaluate($"Array.isArray(tapestry.world.getProperty('{reloadedId}', 'known_recipes') || [])");
        Assert.Equal(true, isArray);

        var length = rt.Evaluate($@"
            (function() {{
                var k = tapestry.world.getProperty('{reloadedId}', 'known_recipes') || [];
                return Array.isArray(k) ? k.length : -1;
            }})()
        ");
        Assert.Equal(2, Convert.ToInt32(length));

        var hasBench = rt.Evaluate($@"
            (function() {{
                var k = tapestry.world.getProperty('{reloadedId}', 'known_recipes') || [];
                var list = Array.isArray(k) ? k : [];
                return list.indexOf('tapestry-tinkers:level-1-bench') >= 0;
            }})()
        ");
        Assert.Equal(true, hasBench);
    }

    [Fact]
    public void PackListStringProperty_SerializedForm_IsCleanSequenceNotTaggedDict()
    {
        var (rt, world, _, serializer) = BuildRuntime();

        var player = new Entity("player", "Tester");
        world.TrackEntity(player);
        var id = player.Id;

        rt.Execute($@"
            var known = ['tapestry-tinkers:level-1-bench', 'tapestry-tinkers:iron-pickaxe'];
            tapestry.world.setProperty('{id}', 'known_recipes', known);
        ");

        var saveData = serializer.ToSaveData(player, Guid.NewGuid(), new List<Entity>());

        // The entity stores the bare key used by JS ("known_recipes"), not the fully-qualified key.
        Assert.True(saveData.Properties.ContainsKey("known_recipes"),
            "Expected 'known_recipes' key in saved properties (bare key as set by JS setProperty)");
        var value = saveData.Properties["known_recipes"];

        // Must NOT be a {type, value} tagged dict.
        Assert.False(value is Dictionary<string, object?>,
            "known_recipes was serialized as a tagged dict — this is the pre-fix bug.");

        // Must be a list type.
        Assert.True(value is List<string> || value is List<object>,
            $"Expected List<string> or List<object>, got {value?.GetType().FullName}");
    }
}
