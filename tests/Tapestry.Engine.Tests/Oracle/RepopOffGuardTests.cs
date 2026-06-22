using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests.Oracle;

public class RepopOffGuardTests
{
    private static (AreaTickService svc, EventBus bus, List<GameEvent> fired) Build(int resetInterval)
    {
        var world = new World();
        var bus = new EventBus();
        var fired = new List<GameEvent>();
        bus.Subscribe("area.tick", e => fired.Add(e));
        var registry = new AreaRegistry();
        registry.Register(new AreaDefinition { Id = "solo-1", ResetInterval = resetInterval });
        var svc = new AreaTickService(world, bus, registry, new ServerConfig());
        return (svc, bus, fired);
    }

    [Fact]
    public void ZeroResetInterval_NeverFiresAreaTick()
    {
        var (svc, _, fired) = Build(resetInterval: 0);
        for (var i = 0; i < 50; i++) { svc.Tick(); }
        Assert.Empty(fired);
    }

    [Fact]
    public void NegativeResetInterval_NeverFiresAreaTick()
    {
        var (svc, _, fired) = Build(resetInterval: -1);
        for (var i = 0; i < 50; i++) { svc.Tick(); }
        Assert.Empty(fired);
    }

    [Fact]
    public void PositiveResetInterval_StillFires()
    {
        var (svc, _, fired) = Build(resetInterval: 3);
        for (var i = 0; i < 10; i++) { svc.Tick(); }
        Assert.NotEmpty(fired);
    }
}
