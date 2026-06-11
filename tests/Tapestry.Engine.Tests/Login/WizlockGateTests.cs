using FluentAssertions;
using Tapestry.Engine.Login;
using Tapestry.Server.Login;

namespace Tapestry.Engine.Tests.Login;

public class WizlockGateTests
{
    [Fact]
    public void WizlockState_DefaultsToUnlocked()
    {
        new WizlockState().Locked.Should().BeFalse();
    }

    [Fact]
    public void Unlocked_AllowsAnyName()
    {
        var gate = new WizlockGate(new WizlockState());

        var result = gate.Check("alice", new FakeConnection());

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public void Locked_RefusesNewCharacterCreation_WithWizlockMessage()
    {
        var gate = new WizlockGate(new WizlockState { Locked = true });

        var result = gate.Check("alice", new FakeConnection());

        result.Allowed.Should().BeFalse();
        result.Message.Should().Be("The game is wizlocked.");
        result.Behavior.Should().Be(LoginBlockBehavior.Disconnect);
    }

    [Fact]
    public void UnlockingAgain_AllowsLogins()
    {
        var state = new WizlockState { Locked = true };
        var gate = new WizlockGate(state);

        state.Locked = false;

        gate.Check("alice", new FakeConnection()).Allowed.Should().BeTrue();
    }
}
