using FluentAssertions;
using Tapestry.Engine.Login;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Login;

public class LoginGateRegistryTests
{
    private static IConnection FakeConn()
    {
        return new FakeConnection();
    }

    [Fact]
    public void RunAll_NoGates_ReturnsAllow()
    {
        var registry = new LoginGateRegistry();
        var result = registry.RunAll("alice", FakeConn());
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public void RunAll_AllGatesAllow_ReturnsAllow()
    {
        var registry = new LoginGateRegistry();
        registry.Register(new AlwaysAllowGate());
        registry.Register(new AlwaysAllowGate());

        var result = registry.RunAll("alice", FakeConn());
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public void RunAll_FirstGateBlocks_ReturnsBlock_SecondGateNotCalled()
    {
        var registry = new LoginGateRegistry();
        var trackingGate = new TrackingBlockGate();
        var secondGate = new TrackingAllowGate();
        registry.Register(trackingGate);
        registry.Register(secondGate);

        var result = registry.RunAll("blocked", FakeConn());

        result.Allowed.Should().BeFalse();
        trackingGate.Called.Should().BeTrue();
        secondGate.Called.Should().BeFalse("second gate must not run after first blocks");
    }

    [Fact]
    public void RunAll_ExecutesInRegistrationOrder()
    {
        var order = new List<int>();
        var registry = new LoginGateRegistry();
        registry.Register(new OrderRecordingGate(1, order, allow: true));
        registry.Register(new OrderRecordingGate(2, order, allow: true));
        registry.Register(new OrderRecordingGate(3, order, allow: true));

        registry.RunAll("alice", FakeConn());

        order.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void RunAll_BlockResult_HasRepromptBehavior()
    {
        var registry = new LoginGateRegistry();
        registry.Register(new MessageBlockGate("That name is taken."));

        var result = registry.RunAll("taken", FakeConn());

        result.Allowed.Should().BeFalse();
        result.Message.Should().Be("That name is taken.");
        result.Behavior.Should().Be(LoginBlockBehavior.Reprompt);
    }

    [Fact]
    public void RunAll_BanResult_HasDisconnectBehavior()
    {
        var registry = new LoginGateRegistry();
        registry.Register(new BanGate("You are banned."));

        var result = registry.RunAll("banned", FakeConn());

        result.Allowed.Should().BeFalse();
        result.Message.Should().Be("You are banned.");
        result.Behavior.Should().Be(LoginBlockBehavior.Disconnect);
    }

    [Fact]
    public void RunAll_NoopResult_HasNullMessageAndDisconnect()
    {
        var registry = new LoginGateRegistry();
        registry.Register(new NoopGate());

        var result = registry.RunAll("ddos", FakeConn());

        result.Allowed.Should().BeFalse();
        result.Message.Should().BeNull();
        result.Behavior.Should().Be(LoginBlockBehavior.Disconnect);
    }

    private class AlwaysAllowGate : ILoginGate
    {
        public LoginGateResult Check(string canonicalName, IConnection connection)
        {
            return LoginGateResult.Allow();
        }
    }

    private class TrackingBlockGate : ILoginGate
    {
        public bool Called { get; private set; }
        public LoginGateResult Check(string canonicalName, IConnection connection)
        {
            Called = true;
            return LoginGateResult.Block("blocked");
        }
    }

    private class TrackingAllowGate : ILoginGate
    {
        public bool Called { get; private set; }
        public LoginGateResult Check(string canonicalName, IConnection connection)
        {
            Called = true;
            return LoginGateResult.Allow();
        }
    }

    private class OrderRecordingGate : ILoginGate
    {
        private readonly int _id;
        private readonly List<int> _order;
        private readonly bool _allow;

        public OrderRecordingGate(int id, List<int> order, bool allow)
        {
            _id = id;
            _order = order;
            _allow = allow;
        }

        public LoginGateResult Check(string canonicalName, IConnection connection)
        {
            _order.Add(_id);
            return _allow ? LoginGateResult.Allow() : LoginGateResult.Block("blocked");
        }
    }

    private class MessageBlockGate : ILoginGate
    {
        private readonly string _msg;
        public MessageBlockGate(string msg)
        {
            _msg = msg;
        }
        public LoginGateResult Check(string canonicalName, IConnection connection)
        {
            return LoginGateResult.Block(_msg);
        }
    }

    private class BanGate : ILoginGate
    {
        private readonly string _msg;
        public BanGate(string msg)
        {
            _msg = msg;
        }
        public LoginGateResult Check(string canonicalName, IConnection connection)
        {
            return LoginGateResult.Ban(_msg);
        }
    }

    private class NoopGate : ILoginGate
    {
        public LoginGateResult Check(string canonicalName, IConnection connection)
        {
            return LoginGateResult.Noop();
        }
    }
}
