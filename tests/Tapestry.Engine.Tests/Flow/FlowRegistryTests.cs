using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Flow;

namespace Tapestry.Engine.Tests.Flow;

public class FlowRegistryTests
{
    private static FlowDefinition MakeFlow(string id, string trigger) => new FlowDefinition
    {
        Id = id,
        Trigger = trigger,
        Steps = Array.Empty<FlowStepDefinition>(),
        OnComplete = _ => new FlowCompletionResult(true)
    };

    [Fact]
    public void Register_then_Get_roundtrip()
    {
        var registry = new FlowRegistry();
        var def = MakeFlow("lf_creation", "new_player_connect");

        registry.Register(def);

        registry.Get("lf_creation").Should().BeSameAs(def);
    }

    [Fact]
    public void Get_returns_null_for_unknown_id()
    {
        var registry = new FlowRegistry();

        registry.Get("missing").Should().BeNull();
    }

    [Fact]
    public void Register_later_definition_same_id_overwrites()
    {
        var registry = new FlowRegistry();
        var first = MakeFlow("lf_creation", "new_player_connect");
        var second = MakeFlow("lf_creation", "new_player_connect");

        registry.Register(first);
        registry.Register(second);

        registry.Get("lf_creation").Should().BeSameAs(second);
    }

    [Fact]
    public void GetByTrigger_returns_matching_flows()
    {
        var registry = new FlowRegistry();
        var a = MakeFlow("flow_a", "trigger_x");
        var b = MakeFlow("flow_b", "trigger_x");
        var c = MakeFlow("flow_c", "trigger_y");

        registry.Register(a);
        registry.Register(b);
        registry.Register(c);

        var result = registry.GetByTrigger("trigger_x");
        result.Should().HaveCount(2);
        result.Should().Contain(a);
        result.Should().Contain(b);
    }

    [Fact]
    public void GetByTrigger_is_case_insensitive()
    {
        var registry = new FlowRegistry();
        registry.Register(MakeFlow("f", "NEW_PLAYER_CONNECT"));

        registry.GetByTrigger("new_player_connect").Should().HaveCount(1);
    }

    [Fact]
    public void GetByTrigger_returns_empty_for_unknown_trigger()
    {
        var registry = new FlowRegistry();

        registry.GetByTrigger("nothing").Should().BeEmpty();
    }

    [Fact]
    public void All_returns_all_registered_flows()
    {
        var registry = new FlowRegistry();
        registry.Register(MakeFlow("a", "t1"));
        registry.Register(MakeFlow("b", "t2"));

        registry.All.Should().HaveCount(2);
    }
}
