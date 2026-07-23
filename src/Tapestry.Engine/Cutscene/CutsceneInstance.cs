namespace Tapestry.Engine.Cutscene;

/// <summary>
/// Live state for one player's in-flight cutscene. Owned and mutated exclusively by
/// <see cref="CutsceneManager"/> -- PlayerSession holds a reference (so HandleInput can route
/// "skip" without knowing cutscene internals) but never touches these fields itself, mirroring
/// how PlayerSession.CurrentFlow delegates to FlowInstance.HandleInput.
/// </summary>
public class CutsceneInstance
{
    public required Guid PlayerId { get; init; }
    public required IReadOnlyList<CutsceneBeat> Beats { get; init; }
    public required bool Skippable { get; init; }

    internal int NextIndex { get; set; }
    internal long NextEmitTick { get; set; }
    internal bool Completed { get; set; }
    internal Action? OnComplete { get; set; }

    /// <summary>Set by CutsceneManager.Play; routes a raw input line while this instance holds
    /// the session. Never null once the instance is handed to PlayerSession.</summary>
    internal Action<string>? InputHandler { get; set; }

    /// <summary>Called from PlayerSession.HandleInput. Input during a cutscene is always
    /// swallowed at this seam -- InputHandler decides only whether "skip" additionally flushes.</summary>
    public void HandleInput(string input)
    {
        InputHandler?.Invoke(input);
    }
}
