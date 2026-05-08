using Tapestry.Engine;
using Tapestry.Engine.Quests;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class QuestModule : IJintApiModule
{
    private readonly QuestService _questService;
    private readonly QuestRegistry _questRegistry;
    private readonly World _world;

    public string Namespace => "quests";

    public QuestModule(QuestService questService, QuestRegistry questRegistry, World world)
    {
        _questService = questService;
        _questRegistry = questRegistry;
        _world = world;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            offer = new Action<string, string>((entityIdStr, questId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return;
                }

                var player = _world.GetEntity(id);
                if (player == null)
                {
                    return;
                }

                _questService.AcceptQuest(player, questId);
            }),

            isActive = new Func<string, string, bool>((entityIdStr, questId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return false;
                }

                return _questService.IsActive(id, questId);
            }),

            isCompleted = new Func<string, string, bool>((entityIdStr, questId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return false;
                }

                return _questService.IsCompleted(id, questId);
            }),

            getState = new Func<string, object?>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return null;
                }

                var state = _questService.GetState(id);
                if (state == null)
                {
                    return null;
                }

                return new
                {
                    active = state.Active.Select(a =>
                    {
                        var def = _questRegistry.Get(a.QuestId);
                        return new
                        {
                            questId = a.QuestId,
                            name = def?.Name ?? a.QuestId,
                            type = def?.Type ?? "side",
                            stageIndex = a.StageIndex,
                            stageCount = def?.Stages.Count ?? 0,
                            objectives = a.Objectives.Select(o =>
                            {
                                var stage = def?.Stages.ElementAtOrDefault(a.StageIndex);
                                var objDef = stage?.Objectives.FirstOrDefault(od => od.Id == o.ObjectiveId);
                                return new
                                {
                                    objectiveId = o.ObjectiveId,
                                    description = objDef?.Description ?? o.ObjectiveId,
                                    current = o.Current,
                                    required = o.Required,
                                    complete = o.Complete,
                                };
                            }).ToArray(),
                        };
                    }).ToArray()
                };
            }),

            abandon = new Action<string, string>((entityIdStr, questId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return;
                }

                _questService.AbandonQuest(id, questId);
            }),

            registerObjectiveType = new Action<string, object>((type, handler) =>
            {
                // Placeholder for custom objective type registration.
                // Full integration requires QuestObjectiveWatcher wiring; no-op for now.
            }),
        };
    }
}
