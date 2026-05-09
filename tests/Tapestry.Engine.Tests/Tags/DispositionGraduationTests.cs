using FluentAssertions;
using Tapestry.Engine.Mobs;

namespace Tapestry.Engine.Tests.Tags;

public class DispositionGraduationTests
{
    [Fact]
    public void MobTemplate_CreateEntity_SetsDisposition()
    {
        var template = new MobTemplate
        {
            Id = "test:goblin",
            Name = "a goblin",
            Type = "mob",
            BaseDisposition = Disposition.Hostile
        };

        var entity = template.CreateEntity();

        entity.Disposition.Should().Be(Disposition.Hostile);
        entity.HasTag("hostile").Should().BeFalse();
    }

    [Fact]
    public void MobTemplate_DefaultDisposition_IsNeutral()
    {
        var template = new MobTemplate
        {
            Id = "test:guard",
            Name = "a guard",
            Type = "npc"
        };

        var entity = template.CreateEntity();

        entity.Disposition.Should().Be(Disposition.Neutral);
    }
}
