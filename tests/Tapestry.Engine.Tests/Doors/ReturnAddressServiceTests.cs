using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests.Doors;

public class ReturnAddressServiceTests
{
    private EventBus _eventBus = null!;
    private ReturnAddressService _service = null!;

    private void Setup()
    {
        _eventBus = new EventBus();
        _service = new ReturnAddressService(_eventBus);
    }

    private Entity MakePlayer()
    {
        return new Entity("player", "Rand");
    }

    [Fact]
    public void SetReturn_StoresRoomId()
    {
        Setup();
        var player = MakePlayer();
        _service.SetReturn(player, "core:inn");
        _service.GetReturn(player).Should().Be("core:inn");
    }

    [Fact]
    public void HasReturn_TrueAfterSet()
    {
        Setup();
        var player = MakePlayer();
        _service.SetReturn(player, "core:inn");
        _service.HasReturn(player).Should().BeTrue();
    }

    [Fact]
    public void HasReturn_FalseBeforeSet()
    {
        Setup();
        var player = MakePlayer();
        _service.HasReturn(player).Should().BeFalse();
    }

    [Fact]
    public void ClearReturn_RemovesAddress()
    {
        Setup();
        var player = MakePlayer();
        _service.SetReturn(player, "core:inn");
        _service.ClearReturn(player);
        _service.HasReturn(player).Should().BeFalse();
        _service.GetReturn(player).Should().BeNull();
    }

    [Fact]
    public void SetReturn_PublishesReturnSetEvent()
    {
        Setup();
        var player = MakePlayer();
        GameEvent? captured = null;
        _eventBus.Subscribe("return.set", e => { captured = e; });

        _service.SetReturn(player, "core:inn");

        captured.Should().NotBeNull();
        captured!.Data["roomId"].Should().Be("core:inn");
    }
}
