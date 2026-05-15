using System;
using System.Collections.Generic;
using System.Linq;

namespace Tapestry.Engine.Quests;

public record QuestMarker(Guid EntityId, string TemplateId, string QuestId);

public class QuestMarkerService
{
    private readonly QuestStateRepository _repo;
    private readonly QuestRegistry _registry;

    public QuestMarkerService(QuestStateRepository repo, QuestRegistry registry)
    {
        _repo = repo;
        _registry = registry;
    }

    public bool HasQuestMarker(Guid playerId, string templateId)
    {
        var state = _repo.GetOrCreate(playerId);
        foreach (var active in state.Active)
        {
            var def = _registry.Get(active.QuestId);
            if (def == null || def.Secret) { continue; }
            if (IsMarkerTarget(def, active, templateId)) { return true; }
        }
        return false;
    }

    public List<QuestMarker> GetQuestMarkersForRoom(Guid playerId, IEnumerable<Entity> entities)
    {
        var result = new List<QuestMarker>();
        var state = _repo.GetOrCreate(playerId);

        foreach (var entity in entities)
        {
            var templateId = entity.GetProperty<string>(CommonProperties.TemplateId);
            if (string.IsNullOrEmpty(templateId)) { continue; }

            foreach (var active in state.Active)
            {
                var def = _registry.Get(active.QuestId);
                if (def == null || def.Secret) { continue; }
                if (IsMarkerTarget(def, active, templateId))
                {
                    result.Add(new QuestMarker(entity.Id, templateId, active.QuestId));
                    break;
                }
            }
        }
        return result;
    }

    private static bool IsMarkerTarget(QuestDefinition def, ActiveQuest active, string templateId)
    {
        if (def.Giver == templateId) { return true; }

        var stage = def.Stages.ElementAtOrDefault(active.StageIndex);
        if (stage == null) { return false; }

        foreach (var obj in stage.Objectives)
        {
            if (obj.Type == "deliver" && obj.Npc == templateId) { return true; }
            if (obj.Type == "collect" && obj.Target == templateId) { return true; }
            // kill objectives intentionally excluded
        }
        return false;
    }
}
