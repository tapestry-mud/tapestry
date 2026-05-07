using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests.Login;

public class LoginPhaseTests
{
    [Fact]
    public void LoginPhase_HasExpectedValues()
    {
        var phases = Enum.GetNames<LoginPhase>();
        phases.Should().Contain(["Connected", "Name", "Password", "SessionTakeover", "Creating", "Playing"]);
    }

    [Fact]
    public void Creating_And_Playing_Preserve_Ordinal_Relationship()
    {
        ((int)LoginPhase.Creating).Should().BeLessThan((int)LoginPhase.Playing);
    }
}
