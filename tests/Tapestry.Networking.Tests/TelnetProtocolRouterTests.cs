using FluentAssertions;
using Tapestry.Networking;

namespace Tapestry.Networking.Tests;

public class TelnetProtocolRouterTests
{
    [Fact]
    public void HandleSubnegotiation_routes_to_registered_handler()
    {
        var router = new TelnetProtocolRouter();
        var handler = new FakeProtocolHandler(201);
        router.Register(handler);

        router.HandleSubnegotiation(201, new byte[] { 1, 2, 3 });

        handler.ReceivedData.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void HandleSubnegotiation_ignores_unknown_option()
    {
        var router = new TelnetProtocolRouter();
        // Should not throw
        router.HandleSubnegotiation(201, new byte[] { 1 });
    }

    [Fact]
    public void GetHandler_returns_typed_handler()
    {
        var router = new TelnetProtocolRouter();
        var handler = new FakeProtocolHandler(201);
        router.Register(handler);

        var result = router.GetHandler<FakeProtocolHandler>(201);

        result.Should().BeSameAs(handler);
    }

    [Fact]
    public void GetHandler_returns_null_for_missing_option()
    {
        var router = new TelnetProtocolRouter();

        var result = router.GetHandler<FakeProtocolHandler>(201);

        result.Should().BeNull();
    }

    [Fact]
    public void Dispose_clears_handlers()
    {
        var router = new TelnetProtocolRouter();
        router.Register(new FakeProtocolHandler(201));
        router.Dispose();

        var result = router.GetHandler<FakeProtocolHandler>(201);
        result.Should().BeNull();
    }

    private class FakeProtocolHandler : IProtocolHandler
    {
        public byte OptionCode { get; }
        public bool IsSessionLong => true;
        public byte[]? ReceivedData { get; private set; }

        public FakeProtocolHandler(byte optionCode)
        {
            OptionCode = optionCode;
        }

        public Task NegotiateAsync(TelnetConnection connection, CancellationToken ct) => Task.CompletedTask;
        public void HandleRemoteDo(TelnetConnection connection) { }

        public void HandleSubnegotiation(byte[] data)
        {
            ReceivedData = data;
        }
    }
}
