using FluentAssertions;
using Tapestry.Scripting.Modules;

namespace Tapestry.Scripting.Tests;

public class UiModuleCapWidthTests
{
    [Fact] public void Off_UsesPreferred() => UiModule.CapWidth(80, 0, 56).Should().Be(80);
    [Fact] public void Wider_StaysPreferred_NoFill() => UiModule.CapWidth(80, 120, 56).Should().Be(80);
    [Fact] public void Narrower_Caps() => UiModule.CapWidth(80, 65, 56).Should().Be(65);
    [Fact] public void BelowMin_FlooredAtMin() => UiModule.CapWidth(80, 40, 56).Should().Be(56);
    [Fact] public void ExactlyMin_Holds() => UiModule.CapWidth(80, 56, 56).Should().Be(56);
}
