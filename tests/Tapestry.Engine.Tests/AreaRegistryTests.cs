using FluentAssertions;
using Tapestry.Engine;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AreaRegistryTests
{
    [Fact]
    public void Register_And_Get_ReturnsDefinition()
    {
        var registry = new AreaRegistry();
        var def = new AreaDefinition { Id = "starter-town", Name = "Starter Town" };

        registry.Register(def);

        registry.Get("starter-town").Should().BeSameAs(def);
    }

    [Fact]
    public void Get_Unknown_ReturnsNull()
    {
        var registry = new AreaRegistry();

        registry.Get("nonexistent").Should().BeNull();
    }

    [Fact]
    public void All_ReturnsAllRegistered()
    {
        var registry = new AreaRegistry();
        registry.Register(new AreaDefinition { Id = "area-one", Name = "Area One" });
        registry.Register(new AreaDefinition { Id = "area-two", Name = "Area Two" });

        registry.All().Should().HaveCount(2);
    }

    [Fact]
    public void Contains_ReturnsTrueForRegistered()
    {
        var registry = new AreaRegistry();
        registry.Register(new AreaDefinition { Id = "my-area", Name = "My Area" });

        registry.Contains("my-area").Should().BeTrue();
        registry.Contains("other-area").Should().BeFalse();
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var registry = new AreaRegistry();
        registry.Register(new AreaDefinition { Id = "Starter-Town", Name = "Starter Town" });

        registry.Get("starter-town").Should().NotBeNull();
        registry.Get("STARTER-TOWN").Should().NotBeNull();
    }
}
