namespace Tapestry.Engine.Quests;

/// <summary>
/// No-op IQuestScriptLoader used when no scripting runtime is registered.
/// All hooks are skipped -- quests work without scripts.
/// </summary>
internal sealed class NullQuestScriptLoader : IQuestScriptLoader
{
    public bool HasScript(string questId) => false;
    public bool CallOnGranted(string questId, Guid playerId) => false;
    public void CallOnObjectiveAdvanced(string questId, Guid playerId, string objectiveId, int current, int required) { }
    public void CallOnCompleted(string questId, Guid playerId) { }
    public void CallOnStageAdvanced(string questId, Guid playerId, int stageIndex) { }
}
