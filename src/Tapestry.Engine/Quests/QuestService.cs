using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private readonly IQuestScriptLoader? _scriptLoader;
    private readonly IQuestRewardDispatcher _rewards;
    private readonly IQuestPersistence _persistence;

    // Tracks player entities by ID so AdvanceObjective/AbandonQuest can resolve them.
    private readonly Dictionary<Guid, Entity> _playerCache = new();

    public QuestService(
        QuestRegistry registry,
        QuestStateRepository stateRepo,
        EventBus eventBus,
        QuestConfig config,
        IQuestScriptLoader? scriptLoader,
        IQuestRewardDispatcher rewards,
        IQuestPersistence persistence)
    {
        _registry = registry;
        _stateRepo = stateRepo;
        _eventBus = eventBus;
        _config = config;
        _scriptLoader = scriptLoader;
        _rewards = rewards;
        _persistence = persistence;
    }

    public AcceptQuestResult AcceptQuest(Entity player, string questId, bool silent = false)
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

        _playerCache[player.Id] = player;

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

        string? bannerText = null;
        if (!def.Secret && !silent)
        {
            var scriptSuppresses = def.Script != null && _scriptLoader != null
                && _scriptLoader.CallOnGranted(questId, player.Id);
            if (!scriptSuppresses)
            {
                bannerText = BuildBanner(def, active);
            }
        }
        else if (def.Script != null && _scriptLoader != null)
        {
            _scriptLoader.CallOnGranted(questId, player.Id);
        }

        var eventData = new Dictionary<string, object?> { ["questId"] = questId };
        if (bannerText != null)
        {
            eventData["bannerText"] = bannerText;
        }

        _eventBus.Publish(new GameEvent
        {
            Type = "quest.started",
            SourceEntityId = player.Id,
            Data = eventData,
        });

        _persistence.Save(player.Name, state);

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

        _scriptLoader?.CallOnObjectiveAdvanced(questId, playerId, objectiveId, obj.Current, obj.Required);

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
            _persistence.Save(GetPlayerName(playerId), state);
            return;
        }

        var def = _registry.Get(questId)!;
        var nextStageIndex = active.StageIndex + 1;

        if (nextStageIndex < def.Stages.Count)
        {
            AdvanceStage(active, def, nextStageIndex, playerId);
            _persistence.Save(GetPlayerName(playerId), state);
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

        _persistence.Save(GetPlayerName(playerId), state);
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

        _scriptLoader?.CallOnStageAdvanced(def.Id, playerId, nextStageIndex);

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

        var player = _playerCache.GetValueOrDefault(playerId);
        if (player != null)
        {
            _rewards.Dispatch(player, def.Rewards);
        }

        _scriptLoader?.CallOnCompleted(questId, playerId);

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

        _persistence.Save(GetPlayerName(playerId), state);
    }

    private bool CheckPrereqs(Entity player, QuestPrereqs prereqs, QuestState state)
    {
        var levelMap = player.GetProperty<Dictionary<string, int>>(ProgressionProperties.Level);
        var level = levelMap != null && levelMap.TryGetValue("main", out var lvl) ? lvl : 0;
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

    private string GetPlayerName(Guid playerId) =>
        _playerCache.TryGetValue(playerId, out var entity) ? entity.Name : playerId.ToString();

    private static string BuildBanner(QuestDefinition quest, ActiveQuest active)
    {
        const int Width = 46;
        var inner = Width - 4;

        var lines = new List<string>();
        lines.Add("+" + new string('-', Width - 2) + "+");

        var titleLeft = "New Quest: " + quest.Name;
        var typeStr = quest.Type ?? "side";
        var titleWidth = inner - 8;
        var typeFormatted = typeStr.PadLeft(6);
        var titleLine = "| " + titleLeft.PadRight(titleWidth) + " (" + typeFormatted + ") |";
        lines.Add(titleLine);
        lines.Add("|" + new string(' ', Width - 2) + "|");

        var stage = quest.Stages?.ElementAtOrDefault(active.StageIndex);
        if (!string.IsNullOrEmpty(stage?.Description))
        {
            foreach (var line in WordWrap(stage.Description, inner))
            {
                lines.Add("| " + line.PadRight(inner) + " |");
            }
            lines.Add("|" + new string(' ', Width - 2) + "|");
        }

        foreach (var obj in active.Objectives)
        {
            var objDef = stage?.Objectives.FirstOrDefault(o => o.Id == obj.ObjectiveId);
            var desc = objDef?.Description ?? obj.ObjectiveId;
            var progress = $"[{obj.Current}/{obj.Required}]";
            var descWidth = inner - progress.Length - 1;
            lines.Add("| " + desc.PadRight(descWidth) + " " + progress + " |");
        }

        lines.Add("|" + new string(' ', Width - 2) + "|");
        var footerText = "Type 'quests' to track progress.";
        lines.Add("| " + footerText.PadRight(inner) + " |");
        lines.Add("+" + new string('-', Width - 2) + "+");

        return "\r\n" + string.Join("\r\n", lines) + "\r\n";
    }

    private static IEnumerable<string> WordWrap(string text, int width)
    {
        var words = text.Split(' ');
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > width && current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
            if (current.Length > 0)
            {
                current.Append(' ');
            }
            current.Append(word);
        }
        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
