using FluentAssertions;
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Tests.Persistence;

public class PropertyTypeRegistryTests
{
    [Fact]
    public void Register_ExactKey_ReturnsType()
    {
        var registry = new PropertyTypeRegistry();
        registry.Register("regen_hp", typeof(int));
        registry.GetType("regen_hp").Should().Be(typeof(int));
    }

    [Fact]
    public void GetType_UnknownKey_ReturnsNull()
    {
        var registry = new PropertyTypeRegistry();
        registry.GetType("unknown").Should().BeNull();
    }

    [Fact]
    public void RegisterPrefix_MatchesDynamicKeys()
    {
        var registry = new PropertyTypeRegistry();
        registry.RegisterPrefix("level:", typeof(int));
        registry.GetType("level:combat").Should().Be(typeof(int));
        registry.GetType("level:crafting").Should().Be(typeof(int));
    }

    [Fact]
    public void ExactMatch_TakesPrecedence_OverPrefix()
    {
        var registry = new PropertyTypeRegistry();
        registry.RegisterPrefix("ac_", typeof(int));
        registry.Register("ac_special", typeof(string));
        registry.GetType("ac_special").Should().Be(typeof(string));
        registry.GetType("ac_slash").Should().Be(typeof(int));
    }

    [Fact]
    public void IsRegistered_ReturnsFalse_ForUnknown()
    {
        var registry = new PropertyTypeRegistry();
        registry.Register("regen_hp", typeof(int));
        registry.IsRegistered("regen_hp").Should().BeTrue();
        registry.IsRegistered("unknown").Should().BeFalse();
    }

    [Fact]
    public void IsRegistered_ReturnsTrue_ForPrefixMatch()
    {
        var registry = new PropertyTypeRegistry();
        registry.RegisterPrefix("xp:", typeof(int));
        registry.IsRegistered("xp:combat").Should().BeTrue();
    }
}
