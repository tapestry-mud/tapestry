using Tapestry.Engine.Cutscene;

namespace Tapestry.Engine.Heartbeat;

/// <summary>
/// Advances every in-flight cutscene one tick (100ms), same shape as SwellClockPulse: paced
/// output pacing lives in the engine, driven by the heartbeat, not hand-rolled schedule callbacks.
/// Same priority tier as SwellClockPulse -- both are paced-output cadence, not gameplay resolution.
/// </summary>
public class CutscenePulse : IPulseHandler
{
    private readonly CutsceneManager _cutscenes;

    public CutscenePulse(CutsceneManager cutscenes)
    {
        _cutscenes = cutscenes;
    }

    public string Name => "CutscenePulse";
    public int Cadence => 1;
    public int Priority => 90;

    public void Execute(PulseContext context)
    {
        _cutscenes.AdvanceAll(context.CurrentTick);
    }
}
