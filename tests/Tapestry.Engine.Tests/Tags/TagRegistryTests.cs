using FluentAssertions;
using Tapestry.Engine.Tags;

namespace Tapestry.Engine.Tests.Tags;

public class TagRegistryTests
{
    [Fact]
    public void RegisterEngineTag_StoresEntry()
    {
        var registry = new TagRegistry();
        registry.RegisterEngineTag("hostile", "Can be targeted in combat", ["mob"]);

        var found = registry.TryResolve("hostile", currentPack: null, out var entry);

        found.Should().BeTrue();
        entry.Name.Should().Be("hostile");
        entry.Scope.Should().Be("engine");
        entry.FullName.Should().Be("hostile");
        entry.IsEngineTag.Should().BeTrue();
        entry.AppliesTo.Should().Contain("mob");
    }

    [Fact]
    public void RegisterEngineTag_RejectsHyphenatedName()
    {
        var registry = new TagRegistry();

        var act = () => registry.RegisterEngineTag("no-get", "desc", ["item"]);

        act.Should().Throw<ArgumentException>().WithMessage("*no_get*");
    }

    [Fact]
    public void RegisterEngineTag_RejectsNonSnakeCase()
    {
        var registry = new TagRegistry();

        var act = () => registry.RegisterEngineTag("NoGet", "desc", ["item"]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterEngineTag_RejectsDuplicateName()
    {
        var registry = new TagRegistry();
        registry.RegisterEngineTag("hostile", "desc", ["mob"]);

        var act = () => registry.RegisterEngineTag("hostile", "desc2", ["mob"]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*hostile*already registered*");
    }

    [Fact]
    public void RegisterPackTag_StoresWithFullPrefix()
    {
        var registry = new TagRegistry();
        registry.RegisterPackTag("dark-forest", "cursed", "Item carries a curse", ["item"]);

        registry.IsKnown("cursed", currentPack: null).Should().BeFalse();
        registry.IsKnown("dark-forest:cursed", currentPack: null).Should().BeTrue();
        registry.IsKnown("cursed", currentPack: "dark-forest").Should().BeTrue();
    }

    [Fact]
    public void RegisterPackTag_CannotRedefineEngineTag()
    {
        var registry = new TagRegistry();
        registry.RegisterEngineTag("hostile", "desc", ["mob"]);

        var act = () => registry.RegisterPackTag("my-pack", "hostile", "override", ["item"]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot redefine*hostile*");
    }

    [Fact]
    public void TryResolve_PrefersEngineTagOverPackBareName()
    {
        var registry = new TagRegistry();
        registry.RegisterEngineTag("shop", "NPC vendor", ["mob"]);

        registry.TryResolve("shop", currentPack: "my-pack", out var entry).Should().BeTrue();
        entry.Scope.Should().Be("engine");
    }

    [Fact]
    public void TryResolve_ResolvesPackTagByFullPrefix()
    {
        var registry = new TagRegistry();
        registry.RegisterPackTag("my-pack", "cursed", "Cursed", ["item"]);

        registry.TryResolve("my-pack:cursed", currentPack: null, out var entry).Should().BeTrue();
        entry.Name.Should().Be("cursed");
        entry.Scope.Should().Be("my-pack");
        entry.FullName.Should().Be("my-pack:cursed");
        entry.IsEngineTag.Should().BeFalse();
    }

    [Fact]
    public void RegisterPackTag_RejectsDuplicateName()
    {
        var registry = new TagRegistry();
        registry.RegisterPackTag("my-pack", "cursed", "desc", ["item"]);

        var act = () => registry.RegisterPackTag("my-pack", "cursed", "desc2", ["item"]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*my-pack:cursed*already registered*");
    }

    [Fact]
    public void GetAll_ReturnsAllRegisteredEntries()
    {
        var registry = new TagRegistry();
        registry.RegisterEngineTag("hostile", "desc", ["mob"]);
        registry.RegisterEngineTag("shop", "desc", ["mob"]);
        registry.RegisterPackTag("my-pack", "cursed", "desc", ["item"]);

        registry.GetAll().Should().HaveCount(3);
    }
}
