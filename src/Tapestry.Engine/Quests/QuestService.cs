using Tapestry.Engine.Progression;
using Tapestry.Shared;

namespace Tapestry.Engine.Quests;

public enum AcceptQuestResult
{
    Accepted,
    AlreadyActive,
    AlreadyCompleted,
    PrereqNotMet,
    CapReached,
    NotFound,
}

public class QuestService
{
    private readonly QuestRegistry _registry;
    private readonly QuestStateRepository _stateRepo;
    private readonly EventBus _eventBus;
    private readonly QuestConfig _config;

    public QuestService(
        QuestRegistry registry,
        QuestStateRepository stateRepo,
        EventBus eventBus,
        QuestConfig config)
    {
        _registry = registry;
        _stateRepo = stateRepo;
        _eventBus = eventBus;
        _config = config;
    }

    public AcceptQuestResult AcceptQuest(Entity player, string questId)
    {
        var def = _registry.Get(questId);
        if (def == null)
        {
            return AcceptQuestResult.NotFound;
        }

        var state = _stateRepo.GetOrCreate(player.Id);

        if (state.Active.Any(q => q.QuestId == questId))
        {
            return AcceptQuestResult.AlreadyActive;
        }

        if (state.Completed.Contains(questId) && !def.Repeatable)
        {
            return AcceptQuestResult.AlreadyCompleted;
        }

        if (!CheckPrereqs(player, def.Prereqs, state))
        {
            return AcceptQuestResult.PrereqNotMet;
        }

        if (def.Abandonable)
        {
            var abandonableActive = state.Active.Count(q =>
            {
                var d = _registry.Get(q.QuestId);
                return d == null || d.Abandonable;
            });
            if (abandonableActive >= _config.ActiveCap)
            {
                return AcceptQuestResult.CapReached;
            }
        }
        // Non-abandonable quests always bypass the cap -- no branch needed.

        var stage = def.Stages[0];
        var active = new ActiveQuest
        {
            QuestId = questId,
            StageIndex = 0,
            Objectives = stage.Objectives.Select(o => new QuestObjectiveState
            {
                ObjectiveId = o.Id,
                Current = 0,
                Required = o.Count,
            }).ToList(),
        };

        state.Active.Add(active);

        _eventBus.Publish(new GameEvent
        {
            Type = "quest.started",
            SourceEntityId = player.Id,
            Data = { ["questId"] = questId },
        });

        return AcceptQuestResult.Accepted;
    }

    public void AdvanceObjective(Guid playerId, string questId, string objectiveId, int amount)
    {
        var state = _stateRepo.Get(playerId);
        if (state == null)
        {
            return;
        }

        var active = state.Active.FirstOrDefault(q => q.QuestId == questId);
        if (active == null)
        {
            return;
        }

        var obj = active.Objectives.FirstOrDefault(o => o.ObjectiveId == objectiveId);
        if (obj == null || obj.Complete)
        {
            return;
        }

        obj.Current = Math.Min(obj.Current + amount, obj.Required);

        _eventBus.Publish(new GameEvent
        {
            Type = "quest.objective.advanced",
            SourceEntityId = playerId,
            Data =
            {
                ["questId"] = questId,
                ["objectiveId"] = objectiveId,
                ["current"] = obj.Current,
                ["required"] = obj.Required,
            },
        });

        if (!active.Objectives.All(o => o.Complete))
        {
            return;
        }

        var def = _registry.Get(questId)!;
        var nextStageIndex = active.StageIndex + 1;

        if (nextStageIndex < def.Stages.Count)
        {
            AdvanceStage(active, def, nextStageIndex, playerId);
        }
        else
        {
            CompleteQuest(playerId, questId, state, active, def);
        }
    }

    public void AdvanceMatchingObjectives(
        Guid playerId,
        string objectiveType,
        Func<QuestObjective, bool> objectiveMatches)
    {
        var state = _stateRepo.Get(playerId);
        if (state == null)
        {
            return;
        }

        foreach (var active in state.Active.ToList())
        {
            var def = _registry.Get(active.QuestId);
            if (def == null)
            {
                continue;
            }

            var stage = def.Stages[active.StageIndex];
            foreach (var objDef in stage.Objectives)
            {
                if (objDef.Type != objectiveType)
                {
                    continue;
                }

                if (!objectiveMatches(objDef))
                {
                    continue;
                }

                AdvanceObjective(playerId, active.QuestId, objDef.Id, 1);
            }
        }
    }

    public void AbandonQuest(Guid playerId, string questId)
    {
        var def = _registry.Get(questId);
        if (def == null || !def.Abandonable)
        {
            return;
        }

        var state = _stateRepo.Get(playerId);
        if (state == null)
        {
            return;
        }

        state.Active.RemoveAll(q => q.QuestId == questId);

        _eventBus.Publish(new GameEvent
        {
            Type = "quest.abandoned",
            SourceEntityId = playerId,
            Data = { ["questId"] = questId },
        });
    }

    public bool IsActive(Guid playerId, string questId) =>
        _stateRepo.Get(playerId)?.Active.Any(q => q.QuestId == questId) == true;

    public bool IsCompleted(Guid playerId, string questId) =>
        _stateRepo.Get(playerId)?.Completed.Contains(questId) == true;

    public QuestState? GetState(Guid playerId) => _stateRepo.Get(playerId);

    private void AdvanceStage(ActiveQuest active, QuestDefinition def, int nextStageIndex, Guid playerId)
    {
        active.StageIndex = nextStageIndex;
        var nextStage = def.Stages[nextStageIndex];
        active.Objectives = nextStage.Objectives.Select(o => new QuestObjectiveState
        {
            ObjectiveId = o.Id,
            Current = 0,
            Required = o.Count,
        }).ToList();

        _eventBus.Publish(new GameEvent
        {
            Type = "quest.stage.advanced",
            SourceEntityId = playerId,
            Data =
            {
                ["questId"] = def.Id,
                ["stageIndex"] = nextStageIndex,
            },
        });
    }

    private void CompleteQuest(Guid playerId, string questId, QuestState state, ActiveQuest active, QuestDefinition def)
    {
        state.Active.Remove(active);
        state.Completed.Add(questId);

        _eventBus.Publish(new GameEvent
        {
            Type = "quest.completed",
            SourceEntityId = playerId,
            Data =
            {
                ["questId"] = questId,
                ["rewardXp"] = def.Rewards.Xp,
                ["rewardGold"] = def.Rewards.Gold,
                ["rewardItems"] = def.Rewards.Items,
                ["rewardAbilities"] = def.Rewards.Abilities,
                ["classUnlock"] = def.Rewards.ClassUnlock,
            },
        });
    }

    private bool CheckPrereqs(Entity player, QuestPrereqs prereqs, QuestState state)
    {
        // Level is stored via ProgressionProperties.Level("main") using entity.GetProperty<int>
        // Default is 0 when unset; treat 0 as 1 to match ProgressionManager behavior
        var level = player.GetProperty<int>(ProgressionProperties.Level("main"));
        if (level == 0)
        {
            level = 1;
        }

        if (level < prereqs.MinLevel)
        {
            return false;
        }

        // Class is stored via CommonProperties.Class ("class") key
        if (prereqs.Class != null)
        {
            var playerClass = player.GetProperty<string>(CommonProperties.Class);
            if (playerClass != prereqs.Class)
            {
                return false;
            }
        }

        if (prereqs.QuestsCompleted.Any(qid => !state.Completed.Contains(qid)))
        {
            return false;
        }

        if (prereqs.QuestsNotCompleted.Any(qid => state.Completed.Contains(qid)))
        {
            return false;
        }

        return true;
    }
}
