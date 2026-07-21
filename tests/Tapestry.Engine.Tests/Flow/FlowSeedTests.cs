using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Flow;

namespace Tapestry.Engine.Tests.Flow;

public class FlowSeedTests
{
    [Fact]
    public void FlowInstance_constructed_with_seed_exposes_it_on_scratch()
    {
        var def = new FlowDefinition
        {
            Id = "t",
            Trigger = "t",
            Steps = new FlowStepDefinition[]
            {
                new InfoStep { Id = "i", Text = (_, _) => "hi" }
            },
            OnComplete = (_, _) => new FlowCompletionResult(true)
        };
        var seed = new Dictionary<string, object?> { ["edit_area"] = "wot:tar-valon" };
        var instance = new FlowInstance(def, new Entity("player", "Rand"), scratchSeed: seed);

        instance.Scratch.Get("edit_area").Should().Be("wot:tar-valon");
    }
}
