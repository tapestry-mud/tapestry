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

    public QuestHandler(
        IGmcpConnectionManager connectionManager,
        QuestService questService,
        QuestRegistry questRegistry,
        EventBus eventBus)
    {
        _connectionManager = connectionManager;
        _questService = questService;
        _questRegistry = questRegistry;
        _eventBus = eventBus;
    }

    public void Configure()
    {
        _eventBus.Subscribe("quest.started", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendQuestList(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("quest.objective.advanced", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("questId", out var questId)) { return; }
            SendQuestUpdate(evt.SourceEntityId.Value, questId?.ToString() ?? "");
        });

        _eventBus.Subscribe("quest.stage.advanced", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("questId", out var questId)) { return; }
            SendQuestUpdate(evt.SourceEntityId.Value, questId?.ToString() ?? "");
        });

        _eventBus.Subscribe("quest.completed", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("questId", out var questId)) { return; }

            var rewardXp = evt.Data.TryGetValue("rewardXp", out var xp) ? xp : null;
            var rewardGold = evt.Data.TryGetValue("rewardGold", out var gold) ? gold : null;
            var rewardItems = evt.Data.TryGetValue("rewardItems", out var items) ? items : null;
            var rewardAbilities = evt.Data.TryGetValue("rewardAbilities", out var abilities) ? abilities : null;
            var classUnlock = evt.Data.TryGetValue("classUnlock", out var cls) ? cls : null;

            var payload = new
            {
                quest_id = questId?.ToString() ?? "",
                reward_xp = rewardXp,
                reward_gold = rewardGold,
                reward_items = rewardItems,
                reward_abilities = rewardAbilities,
                class_unlock = classUnlock,
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
