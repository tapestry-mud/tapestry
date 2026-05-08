using Tapestry.Shared;

namespace Tapestry.Engine.Quests;

public class QuestObjectiveWatcher
{
    private readonly QuestService _questService;
    private readonly QuestStateRepository _stateRepo;
    private readonly EventBus _eventBus;
    private readonly World _world;

    public QuestObjectiveWatcher(
        QuestService questService,
        QuestStateRepository stateRepo,
        EventBus eventBus,
        World world)
    {
        _questService = questService;
        _stateRepo = stateRepo;
        _eventBus = eventBus;
        _world = world;
    }

    public void Start()
    {
        _eventBus.Subscribe("mob.killed", OnMobKilled);
        _eventBus.Subscribe("entity.item.picked_up", OnItemPickedUp);
        _eventBus.Subscribe("entity.item.given", OnItemGiven);
        _eventBus.Subscribe("player.moved", OnPlayerMoved);
    }

    private void OnMobKilled(GameEvent evt)
    {
        if (evt.SourceEntityId == null) { return; }
        if (!evt.Data.TryGetValue("templateId", out var templateId) || templateId is not string mobTemplate) { return; }
        AdvanceMatchingObjectives(evt.SourceEntityId.Value, "kill", obj => obj.Target == mobTemplate);
    }

    private void OnItemPickedUp(GameEvent evt)
    {
        if (evt.SourceEntityId == null) { return; }
        if (!evt.Data.TryGetValue("templateId", out var templateId) || templateId is not string itemTemplate) { return; }
        AdvanceMatchingObjectives(evt.SourceEntityId.Value, "collect", obj => obj.Target == itemTemplate);
    }

    private void OnItemGiven(GameEvent evt)
    {
        if (evt.SourceEntityId == null || evt.TargetEntityId == null) { return; }
        if (!evt.Data.TryGetValue("templateId", out var t) || t is not string itemTemplate) { return; }
        var receiverTemplateId = _world.GetEntity(evt.TargetEntityId.Value)?.GetProperty<string>(CommonProperties.TemplateId);
        AdvanceMatchingObjectives(
            evt.SourceEntityId.Value,
            "deliver",
            obj => obj.Target == itemTemplate && obj.Npc == receiverTemplateId);
    }

    private void OnPlayerMoved(GameEvent evt)
    {
        if (evt.SourceEntityId == null) { return; }
        if (!evt.Data.TryGetValue("new_room_id", out var roomId) || roomId is not string newRoomId) { return; }
        AdvanceMatchingObjectives(evt.SourceEntityId.Value, "visit", obj => obj.Target == newRoomId);
    }

    private void AdvanceMatchingObjectives(
        Guid playerId,
        string objectiveType,
        Func<QuestObjective, bool> objectiveMatches)
    {
        _questService.AdvanceMatchingObjectives(playerId, objectiveType, objectiveMatches);
    }
}
