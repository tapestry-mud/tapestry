using FluentAssertions;

namespace Tapestry.Engine.Tests.Tags;

public class RolesGraduationTests
{
    [Fact]
    public void Entity_HasRole_WorksForAdmin()
    {
        var entity = new Entity("player", "Travis");
        entity.AddRole("admin");

        entity.HasRole("admin").Should().BeTrue();
        entity.HasTag("admin").Should().BeFalse();
    }

    [Fact]
    public void Entity_HasRole_WorksForBuilder()
    {
        var entity = new Entity("player", "Travis");
        entity.AddRole("builder");

        entity.HasRole("builder").Should().BeTrue();
        entity.HasTag("builder").Should().BeFalse();
    }
}
