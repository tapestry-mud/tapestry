using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Cutscene;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

/// <summary>
/// Scripting-facing surface for the Layer-2 cutscene player (output-cadence-cutscenes spec,
/// section 3). Content authors a beat sequence as data; the engine (Tapestry.Engine.Cutscene.
/// CutsceneManager) owns pacing and the prompt-hold lifecycle. See types/tapestry-engine.d.ts
/// for the pack-facing shape.
/// </summary>
public class CutsceneModule : IJintApiModule
{
    private readonly CutsceneManager _cutscenes;
    private readonly GameLoop _gameLoop;

    public CutsceneModule(CutsceneManager cutscenes, GameLoop gameLoop)
    {
        _cutscenes = cutscenes;
        _gameLoop = gameLoop;
    }

    public string Namespace => "cutscene";

    public object Build(JintEngine jint)
    {
        return new
        {
            // play(playerEntityId, beats, options, onComplete)
            //   beats: [{ text, pauseAfter }, ...] -- pauseAfter in ticks (100ms each), optional
            //     per beat (CutsceneManager.DefaultPauseAfterTicks fills a missing value).
            //   options: { skippable: bool }
            //   onComplete: function() -- fires exactly once, naturally or via skip.
            play = new Action<string, JsValue, JsValue, JsValue>((playerIdStr, beatsVal, optsVal, onCompleteVal) =>
            {
                if (!Guid.TryParse(playerIdStr, out var playerId))
                {
                    return;
                }

                var beats = ParseBeats(beatsVal);

                var skippable = false;
                if (optsVal is ObjectInstance optsObj)
                {
                    var skippableVal = optsObj.Get("skippable");
                    skippable = skippableVal.Type == Types.Boolean && (bool)skippableVal.ToObject()!;
                }

                Action? onComplete = null;
                if (onCompleteVal != null && onCompleteVal.Type != Types.Undefined && onCompleteVal.Type != Types.Null)
                {
                    var captured = onCompleteVal;
                    onComplete = () => jint.Invoke(captured);
                }

                _cutscenes.Play(playerId, beats, skippable, _gameLoop.TickCount, onComplete);
            }),

            isActive = new Func<string, bool>(playerIdStr =>
                Guid.TryParse(playerIdStr, out var playerId) && _cutscenes.IsActive(playerId))
        };
    }

    private static List<CutsceneBeat> ParseBeats(JsValue beatsVal)
    {
        var result = new List<CutsceneBeat>();
        if (beatsVal is not JsArray arr)
        {
            return result;
        }

        for (uint i = 0; i < arr.Length; i++)
        {
            if (arr[(int)i] is not ObjectInstance obj)
            {
                continue;
            }

            var textVal = obj.Get("text");
            var text = textVal.Type == Types.String ? textVal.ToString() : "";

            var pauseVal = obj.Get("pauseAfter");
            var pause = pauseVal.Type == Types.Number
                ? (int)(double)pauseVal.ToObject()!
                : CutsceneManager.DefaultPauseAfterTicks;

            result.Add(new CutsceneBeat(text, pause));
        }

        return result;
    }
}
