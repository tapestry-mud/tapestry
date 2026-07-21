using FluentAssertions;
using Tapestry.Engine.Flow;

namespace Tapestry.Engine.Tests.Flow;

public class FlowScratchTests
{
    [Fact]
    public void Get_returns_null_for_absent_key()
    {
        var scratch = new FlowScratch();
        scratch.Get("missing").Should().BeNull();
    }

    [Fact]
    public void Has_is_false_for_absent_key_and_true_after_set()
    {
        var scratch = new FlowScratch();
        scratch.Has("k").Should().BeFalse();
        scratch.Set("k", "v");
        scratch.Has("k").Should().BeTrue();
    }

    [Fact]
    public void Set_then_Get_preserves_real_type()
    {
        var scratch = new FlowScratch();
        scratch.Set("confirmed", true);
        scratch.Get("confirmed").Should().BeOfType<bool>().And.Be(true);
        scratch.Set("count", 3);
        scratch.Get("count").Should().BeOfType<int>().And.Be(3);
    }

    [Fact]
    public void Set_overwrites_existing_value()
    {
        var scratch = new FlowScratch();
        scratch.Set("k", "first");
        scratch.Set("k", "second");
        scratch.Get("k").Should().Be("second");
    }

    [Fact]
    public void Constructor_copies_seed_dictionary()
    {
        var seed = new Dictionary<string, object?> { ["edit_area"] = "wot:tar-valon" };
        var scratch = new FlowScratch(seed);
        scratch.Has("edit_area").Should().BeTrue();
        scratch.Get("edit_area").Should().Be("wot:tar-valon");
    }

    [Fact]
    public void Constructor_seed_is_copied_not_aliased()
    {
        var seed = new Dictionary<string, object?> { ["a"] = 1 };
        var scratch = new FlowScratch(seed);
        seed["a"] = 999; // mutate the source after construction
        scratch.Get("a").Should().Be(1); // store kept its own copy
    }
}
