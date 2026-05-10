using FluentAssertions;
using Tapestry.Engine.Tags;
using Tapestry.Scripting.Tags;

namespace Tapestry.Scripting.Tests.Tags;

public class TagsFileLoaderTests
{
    [Fact]
    public void LoadIntoRegistry_LoadsEngineTagsFromTapestryCore()
    {
        var dir = MakeTempDir("tapestry-core", """
            tags:
              hostile:
                description: "Can be targeted in combat"
                applies_to: [mob]
              shop:
                description: "NPC operates as a vendor"
                applies_to: [mob]
            """);
        try
        {
            var registry = new TagRegistry();
            TagsFileLoader.LoadIntoRegistry(dir, "core", registry);

            registry.IsKnown("hostile", null).Should().BeTrue();
            registry.IsKnown("shop", null).Should().BeTrue();
            registry.GetAll().Should().HaveCount(2);
            registry.GetAll().Should().OnlyContain(e => e.IsEngineTag);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadIntoRegistry_SkipsWhenNoTagsFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tapestry-notags-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var registry = new TagRegistry();
            TagsFileLoader.LoadIntoRegistry(dir, "my-pack", registry);

            registry.GetAll().Should().BeEmpty();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadIntoRegistry_LoadsPackTagsWithPackNameScope()
    {
        var dir = MakeTempDir("my-pack", """
            tags:
              cursed:
                description: "Item carries a curse"
                applies_to: [item]
            """);
        try
        {
            var registry = new TagRegistry();
            TagsFileLoader.LoadIntoRegistry(dir, "my-pack", registry);

            registry.IsKnown("cursed", "my-pack").Should().BeTrue();
            registry.IsKnown("my-pack:cursed", null).Should().BeTrue();
            registry.IsKnown("cursed", null).Should().BeFalse();
            registry.TryResolve("cursed", "my-pack", out var entry);
            entry.Scope.Should().Be("my-pack");
            entry.FullName.Should().Be("my-pack:cursed");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadIntoRegistry_ThrowsOnHyphenatedTagName()
    {
        var dir = MakeTempDir("my-pack", """
            tags:
              no-get:
                description: "Cannot be picked up"
                applies_to: [item]
            """);
        try
        {
            var registry = new TagRegistry();
            var act = () => TagsFileLoader.LoadIntoRegistry(dir, "my-pack", registry);

            act.Should().Throw<ArgumentException>().WithMessage("*no_get*");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadIntoRegistry_PopulatesAppliesToList()
    {
        var dir = MakeTempDir("tapestry-core", """
            tags:
              no_regen:
                description: "Exempt from regeneration"
                applies_to: [mob, player]
            """);
        try
        {
            var registry = new TagRegistry();
            TagsFileLoader.LoadIntoRegistry(dir, "core", registry);

            registry.TryResolve("no_regen", null, out var entry);
            entry.AppliesTo.Should().Contain("mob");
            entry.AppliesTo.Should().Contain("player");
        }
        finally { Directory.Delete(dir, true); }
    }

    private static string MakeTempDir(string packName, string tagsYaml)
    {
        var dir = Path.Combine(Path.GetTempPath(),
            $"tapestry-{packName}-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "tags.yml"), tagsYaml);
        return dir;
    }
}
