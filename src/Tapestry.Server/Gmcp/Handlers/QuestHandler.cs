using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Shared;

namespace Tapestry.Server.Gmcp.Handlers;

public class QuestHandler : IGmcpPackageHandler
{
    public string Name => "Quest";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Quest.List", "Quest.Update", "Quest.Complete", "Quest.Abandon" };

    private readonly IGmcpConnectionManager _connectionManager;
    private readonly QuestService _questService;
    private readonly QuestRegistry _questRegistry;
    private readonly EventBus _eventBus;
    private readonly NotificationQueue _notificationQueue;

    public QuestHandler(
        IGmcpConnectionManager connectionManager,
        QuestService questService,
        QuestRegistry questRegistry,
        EventBus eventBus,
        NotificationQueue notificationQueue)
    {
        _connectionManager = connectionManager;
        _questService = questService;
        _questRegistry = questRegistry;
        _eventBus = eventBus;
        _notificationQueue = notificationQueue;
    }

    public void Configure()
    {
        _eventBus.Subscribe("quest.started", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendQuestList(evt.SourceEntityId.Value);

            if (evt.Data.TryGetValue("bannerText", out var banner) && banner is string text)
            {
                _notificationQueue.Enqueue(evt.SourceEntityId.Value,
                    new Notification("quest_started", 50, text));
            }
        });

        _eventBus.Subscribe("quest.objective.advanced", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("questId", out var questIdObj)) { return; }
            var questId = questIdObj?.ToString() ?? "";
            SendQuestUpdate(evt.SourceEntityId.Value, questId);

            if (!evt.Data.TryGetValue("current", out var cur) || !evt.Data.TryGetValue("required", out var req)) { return; }
            if (!int.TryParse(cur?.ToString(), out var current) || !int.TryParse(req?.ToString(), out var required)) { return; }
            if (current >= required) { return; }

            var objId = evt.Data.TryGetValue("objectiveId", out var oid) ? oid?.ToString() : null;
            var def = _questRegistry.Get(questId);
            var state = _questService.GetState(evt.SourceEntityId.Value);
            var active = state?.Active.FirstOrDefault(q => q.QuestId == questId);
            var stage = def?.Stages.ElementAtOrDefault(active?.StageIndex ?? 0);
            var objDef = stage?.Objectives.FirstOrDefault(o => o.Id == objId);
            var desc = objDef?.Description ?? objId ?? "objective";

            _notificationQueue.Enqueue(evt.SourceEntityId.Value,
                new Notification("quest_progress", 50,
                    $"\r\n<npc>[Quest]</npc> {desc} [{current}/{required}]\r\n"));
        });

        _eventBus.Subscribe("quest.stage.advanced", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("questId", out var questIdObj)) { return; }
            var questId = questIdObj?.ToString() ?? "";
            SendQuestUpdate(evt.SourceEntityId.Value, questId);

            if (!evt.Data.TryGetValue("stageIndex", out var stageIdxObj)) { return; }
            if (!int.TryParse(stageIdxObj?.ToString(), out var stageIndex)) { return; }
            var def = _questRegistry.Get(questId);
            var stage = def?.Stages.ElementAtOrDefault(stageIndex);
            if (stage?.Description == null) { return; }

            _notificationQueue.Enqueue(evt.SourceEntityId.Value,
                new Notification("quest_stage", 50,
                    $"\r\n<npc>[Quest: {def!.Name}]</npc> {stage.Description}\r\n"));
        });

        _eventBus.Subscribe("quest.completed", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("questId", out var questIdObj)) { return; }
            var questId = questIdObj?.ToString() ?? "";

            var def = _questRegistry.Get(questId);
            var questName = def?.Name ?? questId;
            var xp = evt.Data.TryGetValue("rewardXp", out var xpVal) ? Convert.ToInt32(xpVal) : 0;
            var gold = evt.Data.TryGetValue("rewardGold", out var goldVal) ? Convert.ToInt32(goldVal) : 0;
            var cls = evt.Data.TryGetValue("classUnlock", out var clsVal) ? clsVal?.ToString() : null;

            var msg = $"\r\n<highlight>[Quest Complete]</highlight> {questName}";
            if (xp > 0) { msg += $"  {xp} XP"; }
            if (gold > 0) { msg += $"  {gold} gold"; }
            if (!string.IsNullOrEmpty(cls))
            {
                var shortClass = cls!.Contains(':') ? cls[(cls.LastIndexOf(':') + 1)..] : cls;
                msg += $"  Class: {shortClass}";
            }
            msg += "\r\n";

            _notificationQueue.Enqueue(evt.SourceEntityId.Value,
                new Notification("quest_complete", 50, msg,
                    "Notification.Show",
                    new
                    {
                        type = "quest_complete",
                        title = questName,
                        body = string.Join("  ", new[] {
                            xp > 0 ? $"{xp} XP" : null,
                            gold > 0 ? $"{gold} gold" : null,
                            !string.IsNullOrEmpty(cls) ? $"Class: {(cls!.Contains(':') ? cls[(cls.LastIndexOf(':') + 1)..] : cls)}" : null
                        }.Where(s => s != null)),
                        priority = 50
                    }));

            var payload = new
            {
                quest_id = questId,
                reward_xp = xp,
                reward_gold = gold,
                class_unlock = cls,
            };
            _connectionManager.Send(evt.SourceEntityId.Value, "Quest.Complete", payload);
        });

        _eventBus.Subscribe("quest.abandoned", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("questId", out var questId)) { return; }

            var payload = new
            {
                quest_id = questId?.ToString() ?? "",
            };

            _connectionManager.Send(evt.SourceEntityId.Value, "Quest.Abandon", payload);
        });
    }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        var state = _questService.GetState(e.Id);
        if (state == null) { return; }
        _connectionManager.Send(connectionId, "Quest.List", BuildQuestListPayload(state));
    }

    private void SendQuestList(Guid entityId)
    {
        var state = _questService.GetState(entityId);
        if (state == null) { return; }
        _connectionManager.Send(entityId, "Quest.List", BuildQuestListPayload(state));
    }

    private void SendQuestUpdate(Guid entityId, string questId)
    {
        var state = _questService.GetState(entityId);
        var active = state?.Active.FirstOrDefault(q => q.QuestId == questId);
        if (active == null) { return; }

        var payload = BuildActiveQuestPayload(active);
        _connectionManager.Send(entityId, "Quest.Update", payload);
    }

    private object BuildQuestListPayload(QuestState state)
    {
        return new
        {
            active = state.Active.Select(BuildActiveQuestPayload).ToList(),
            completed = state.Completed,
        };
    }

    private object BuildActiveQuestPayload(ActiveQuest active)
    {
        var def = _questRegistry.Get(active.QuestId);
        return new
        {
            quest_id = active.QuestId,
            name = def?.Name ?? active.QuestId,
            type = def?.Type ?? "side",
            stage_index = active.StageIndex,
            stage_count = def?.Stages.Count ?? 0,
            objectives = active.Objectives.Select(o => new
            {
                objective_id = o.ObjectiveId,
                current = o.Current,
                required = o.Required,
                complete = o.Complete,
            }).ToList(),
        };
    }
}
