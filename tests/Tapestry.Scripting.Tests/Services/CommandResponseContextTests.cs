using Tapestry.Scripting.Services;
using Xunit;

namespace Tapestry.Scripting.Tests.Services;

public class CommandResponseContextTests
{
    [Fact]
    public void IsSuppressed_returns_false_for_unknown_entity()
    {
        var ctx = new CommandResponseContext();
        Assert.False(ctx.IsSuppressed(Guid.NewGuid()));
    }

    [Fact]
    public void IsSuppressed_returns_true_after_suppress()
    {
        var ctx = new CommandResponseContext();
        var entityId = Guid.NewGuid();
        ctx.Suppress(entityId);
        Assert.True(ctx.IsSuppressed(entityId));
    }

    [Fact]
    public void IsSuppressed_returns_false_after_reset()
    {
        var ctx = new CommandResponseContext();
        var entityId = Guid.NewGuid();
        ctx.Suppress(entityId);
        ctx.Reset(entityId);
        Assert.False(ctx.IsSuppressed(entityId));
    }

    [Fact]
    public void Suppress_affects_only_the_target_entity()
    {
        var ctx = new CommandResponseContext();
        var e1 = Guid.NewGuid();
        var e2 = Guid.NewGuid();
        ctx.Suppress(e1);
        Assert.True(ctx.IsSuppressed(e1));
        Assert.False(ctx.IsSuppressed(e2));
    }

    [Fact]
    public void Reset_on_unsuppressed_entity_is_a_no_op()
    {
        var ctx = new CommandResponseContext();
        var entityId = Guid.NewGuid();
        ctx.Reset(entityId); // must not throw
        Assert.False(ctx.IsSuppressed(entityId));
    }
}
