using FluentAssertions;
using Tapestry.Engine.Tags;

namespace Tapestry.Engine.Tests.Tags;

public class EngineTagsTests
{
    [Fact]
    public void EngineTags_AreRegistered()
    {
        var tags = new TagRegistry();
        EngineTags.Register(tags);
        foreach (var t in new[]
        {
            "no_kill", "safe", "no_flee", "no_regen", "fixture", "no_get",
            "corpse", "player_corpse", "entry_point", "skill_trainer",
            "linkdead", "fill_source"
        })
        {
            tags.TryResolve(t, "tapestry-engine", out _).Should().BeTrue($"{t} not registered");
        }
    }

    [Fact]
    public void EngineTags_AreOwnedByEngineScope()
    {
        var tags = new TagRegistry();
        EngineTags.Register(tags);

        tags.TryResolve(EngineTags.NoKill, currentPack: null, out var entry).Should().BeTrue();
        entry.IsEngineTag.Should().BeTrue();
        entry.Scope.Should().Be("engine");
    }
}
