using FluentAssertions;
using Tapestry.Engine.Login;
using Tapestry.Server.Login;

namespace Tapestry.Engine.Tests.Login;

public class ReservedNameGateTests
{
    private readonly ReservedNameGate _gate = new();

    [Theory]
    [InlineData("self")]
    [InlineData("me")]
    [InlineData("all")]
    [InlineData("here")]
    [InlineData("nobody")]
    [InlineData("admin")]
    [InlineData("system")]
    public void Blocks_ReservedNames(string name)
    {
        var result = _gate.Check(name, new FakeConnection());
        result.Allowed.Should().BeFalse();
        result.Message.Should().Be("That name is reserved. Try another.");
        result.Behavior.Should().Be(LoginBlockBehavior.Reprompt);
    }

    [Theory]
    [InlineData("Self")]
    [InlineData("ME")]
    [InlineData("Admin")]
    [InlineData("SYSTEM")]
    [InlineData("Nobody")]
    public void Blocks_ReservedNames_CaseInsensitive(string name)
    {
        var result = _gate.Check(name, new FakeConnection());
        result.Allowed.Should().BeFalse();
        result.Message.Should().Be("That name is reserved. Try another.");
        result.Behavior.Should().Be(LoginBlockBehavior.Reprompt);
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("Travis")]
    [InlineData("Krakus")]
    [InlineData("selfie")]
    [InlineData("admirable")]
    public void Allows_NonReservedNames(string name)
    {
        var result = _gate.Check(name, new FakeConnection());
        result.Allowed.Should().BeTrue();
    }
}
