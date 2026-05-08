using FluentAssertions;
using Tapestry.Server.Gmcp;

namespace Tapestry.Engine.Tests.Gmcp;

public class DirtyVitalsBatcherTests
{
    [Fact]
    public void MarkDirty_ThenFlush_CallsCallbackForMarkedEntity()
    {
        var batcher = new DirtyVitalsBatcher();
        var flushed = new List<Guid>();
        batcher.SetFlushCallback(id => flushed.Add(id));

        var entityId = Guid.NewGuid();
        batcher.MarkDirty(entityId);
        batcher.FlushDirtyVitals();

        flushed.Should().Contain(entityId);
    }

    [Fact]
    public void MarkDirty_SameEntityTwice_FlushCallsCallbackOnce()
    {
        var batcher = new DirtyVitalsBatcher();
        var flushed = new List<Guid>();
        batcher.SetFlushCallback(id => flushed.Add(id));

        var entityId = Guid.NewGuid();
        batcher.MarkDirty(entityId);
        batcher.MarkDirty(entityId);
        batcher.FlushDirtyVitals();

        flushed.Should().HaveCount(1);
    }

    [Fact]
    public void FlushDirtyVitals_ClearsDirtySet_SecondFlushIsNoOp()
    {
        var batcher = new DirtyVitalsBatcher();
        var callCount = 0;
        batcher.SetFlushCallback(_ => callCount++);

        var entityId = Guid.NewGuid();
        batcher.MarkDirty(entityId);
        batcher.FlushDirtyVitals();
        batcher.FlushDirtyVitals();

        callCount.Should().Be(1);
    }

    [Fact]
    public void FlushDirtyVitals_WithNoCallback_DoesNotThrow()
    {
        var batcher = new DirtyVitalsBatcher();
        batcher.MarkDirty(Guid.NewGuid());

        var act = () => batcher.FlushDirtyVitals();

        act.Should().NotThrow();
    }

    [Fact]
    public void FlushDirtyVitals_WithEmptySet_DoesNotInvokeCallback()
    {
        var batcher = new DirtyVitalsBatcher();
        var called = false;
        batcher.SetFlushCallback(_ => called = true);

        batcher.FlushDirtyVitals();

        called.Should().BeFalse();
    }
}
