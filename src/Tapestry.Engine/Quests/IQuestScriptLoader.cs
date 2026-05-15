using System;

namespace Tapestry.Engine.Quests;

public interface IQuestScriptLoader
{
    bool HasScript(string questId);
    // Returns true if the hook returned true (suppress banner)
    bool CallOnGranted(string questId, Guid playerId);
    void CallOnObjectiveAdvanced(string questId, Guid playerId, string objectiveId, int current, int required);
    void CallOnCompleted(string questId, Guid playerId);
    void CallOnStageAdvanced(string questId, Guid playerId, int stageIndex);
}
