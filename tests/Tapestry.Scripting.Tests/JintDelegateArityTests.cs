using FluentAssertions;
using Jint;
using Jint.Native;

using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Tests;

/// <summary>
/// Pins the Jint interop behavior the sendRoomDescription binding (and brief mode's
/// cross-version compatibility story) relies on:
///  - a JS call with FEWER args than the CLR delegate arity still invokes it, padding the
///    missing arg with CLR NULL - not JsValue.Undefined - so the binding's null check is
///    load-bearing (published core calling sendRoomDescription(entityId) against the new
///    two-arg binding stays a full render instead of throwing);
///  - a JS call with MORE args than the delegate arity ignores the extras
///    (new core calling sendRoomDescription(entityId, true) against an OLD engine's
///    Action&lt;string&gt; binding renders full instead of throwing).
/// If a Jint upgrade breaks either, these tests fail before a player does.
/// </summary>
public class JintDelegateArityTests
{
    [Fact]
    public void CallWithFewerArgs_InvokesDelegate_PaddingMissingArgWithNull()
    {
        var engine = new JintEngine();
        var ran = false;
        JsValue? seen = JsValue.Undefined; // sentinel: overwritten by the call
        engine.SetValue("fn", new Action<string, JsValue?>((_, second) => { ran = true; seen = second; }));

        engine.Execute("fn('only-one-arg');");

        ran.Should().BeTrue();
        // Jint 4.9.3 pads the missing arg with CLR null (NOT JsValue.Undefined) - this is
        // why the sendRoomDescription binding's null check is load-bearing back-compat.
        seen.Should().BeNull();
    }

    [Fact]
    public void CallWithExtraArgs_IgnoresTheExtras()
    {
        var engine = new JintEngine();
        string? seen = null;
        engine.SetValue("fn", new Action<string>(only => seen = only));

        var act = () => engine.Execute("fn('kept', true);");

        act.Should().NotThrow();
        seen.Should().Be("kept");
    }

    [Fact]
    public void BooleanSecondArg_ArrivesAsJsBoolean()
    {
        var engine = new JintEngine();
        JsValue? seen = null;
        engine.SetValue("fn", new Action<string, JsValue>((_, second) => seen = second));

        engine.Execute("fn('id', true);");

        var value = seen!;
        (value.IsBoolean() && value.AsBoolean()).Should().BeTrue();
    }
}
