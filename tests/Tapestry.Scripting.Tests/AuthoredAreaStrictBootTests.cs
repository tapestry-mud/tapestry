using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Scripting;
using Tapestry.Scripting.Authoring;
using Tapestry.Scripting.Interop;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests;

/// <summary>
/// Task 8.1 — End-to-end "strict boot" guard for the authored-area path.
/// Proves that an <c>area.yaml</c> side-car (all four text fields populated) plus one
/// authored room under it load cleanly and produce ZERO validation issues.
/// Exercises: AuthoredAreaLoader -> AuthoredRoomLoader -> PackValidator.Validate().
/// </summary>
public class AuthoredAreaStrictBootTests
{
    // ---------- fixture setup helpers ----------

    private static string CreateTempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tap-strictboot-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteAreaSideCar(string root)
    {
        var areaDir = Path.Combine(root, "road-to-tar-valon");
        Directory.CreateDirectory(areaDir);
        File.WriteAllText(Path.Combine(areaDir, "area.yaml"),
            "area:\n" +
            "  id: road-to-tar-valon\n" +
            "  name: The Road to Tar Valon\n" +
            "  short: A dusty road winding toward the great city.\n" +
            "  description: |\n" +
            "    The hard-packed road stretches east and west across the plain,\n" +
            "    dotted with waymarkers worn smooth by generations of travelers.\n" +
            "  theme: Jordanian high fantasy — gilded Aes Sedai civilization meets wild frontier.\n" +
            "  lore: |\n" +
            "    The Queen's Road was first laid during the Trolloc Wars as a supply\n" +
            "    corridor between Tar Valon and the eastern keeps.\n");
    }

    private static void WriteRoomSideCar(string root)
    {
        var roomsDir = Path.Combine(root, "road-to-tar-valon", "rooms");
        Directory.CreateDirectory(roomsDir);
        File.WriteAllText(Path.Combine(roomsDir, "the-waygate.yaml"),
            "id: \"road-to-tar-valon:the-waygate\"\n" +
            "area: road-to-tar-valon\n" +
            "name: The Waygate\n" +
            "description: |\n" +
            "  An ancient stone arch stands here, etched with the Great Serpent.\n");
    }

    // ---------- validator factory ----------

    private static PackValidator BuildValidator(World world)
    {
        var items = new ItemRegistry();
        var spawner = new SpawnManager(world, new EventBus(), new LootTableResolver(), items);
        var tags = new TagRegistry();
        tags.SetDependencyResolver(_ => System.Linq.Enumerable.Empty<string>());
        var props = new PropertyRegistry();
        CommonProperties.Register(props);

        return new PackValidator(
            spawner,
            items,
            world,
            NullLogger<PackValidator>.Instance,
            new AbilityRegistry(),
            new CommandRegistry(),
            tags,
            new EmptyManifestProvider(),
            props,
            new PackDependencyGraph(),
            new PackExportRegistry(),
            new InteropCallSiteRegistry());
    }

    // ---------- the test ----------

    [Fact]
    public void AuthoredArea_And_Room_SideCars_Produce_Zero_Validation_Issues()
    {
        var root = CreateTempRoot();
        try
        {
            WriteAreaSideCar(root);
            WriteRoomSideCar(root);

            // Load the authored area side-car into a fresh AreaRegistry.
            var areaRegistry = new AreaRegistry();
            new AuthoredAreaLoader(root, areaRegistry, NullLogger<AuthoredAreaLoader>.Instance).Load();

            // Assert the area was registered with all four text fields present.
            var area = areaRegistry.Get("road-to-tar-valon");
            area.Should().NotBeNull("area.yaml should have been discovered");
            area!.Short.Should().NotBeNullOrWhiteSpace("short text field must be populated");
            area.Description.Should().NotBeNullOrWhiteSpace("description text field must be populated");
            area.Theme.Should().NotBeNullOrWhiteSpace("theme text field must be populated");
            area.Lore.Should().NotBeNullOrWhiteSpace("lore text field must be populated");

            // Load the authored room side-car into a fresh World.
            var world = new World();
            new AuthoredRoomLoader(
                world,
                NullLogger<AuthoredRoomLoader>.Instance,
                root,
                new PropertyRegistry(),
                new TagRegistry()).Load();

            var room = world.GetRoom("road-to-tar-valon:the-waygate");
            room.Should().NotBeNull("room side-car should have been discovered");
            room!.Name.Should().Be("The Waygate");

            // Run the full pack validation pass — must not throw.
            var validator = BuildValidator(world);
            var act = () => validator.Validate();
            act.Should().NotThrow("an authored area + room side-car set must strict-boot with zero validation issues");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

// Minimal manifest provider — no packs loaded (authored-only areas carry no manifest).
file sealed class EmptyManifestProvider : IPackManifestProvider
{
    public IReadOnlyList<PackManifest> LoadedPacks { get; } = new List<PackManifest>();
}
