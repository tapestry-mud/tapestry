using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class QuestModule : IJintApiModule
{
    private readonly QuestService _questService;
    private readonly QuestRegistry _questRegistry;
    private readonly World _world;
    private readonly QuestScriptLoader _scriptLoader;
    private readonly QuestMarkerService _questMarkerService;

    public string Namespace => "quests";

    public QuestModule(QuestService questService, QuestRegistry questRegistry, World world, QuestScriptLoader scriptLoader, QuestMarkerService questMarkerService)
    {
        _questService = questService;
        _questRegistry = questRegistry;
        _world = world;
        _scriptLoader = scriptLoader;
        _questMarkerService = questMarkerService;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            offer = new Func<JsValue, JsValue, JsValue, JsValue>((entityArg, questArg, optionsArg) =>
            {
                var entityIdStr = entityArg.ToString();
                var questId = questArg.ToString();

                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return JsValue.Undefined;
                }

                var player = _world.GetEntity(id);
                if (player == null)
                {
                    return JsValue.Undefined;
                }

                var silent = false;
                if (optionsArg is ObjectInstance opts)
                {
                    var silentProp = opts.Get("silent");
                    if (silentProp.Type == Types.Boolean)
                    {
                        silent = TypeConverter.ToBoolean(silentProp);
                    }
                }

                _questService.AcceptQuest(player, questId, silent: silent);
                return JsValue.Undefined;
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
                        var stage = def?.Stages.ElementAtOrDefault(a.StageIndex);
                        return new
                        {
                            questId = a.QuestId,
                            name = def?.Name ?? a.QuestId,
                            type = def?.Type ?? "side",
                            stageIndex = a.StageIndex,
                            stageCount = def?.Stages.Count ?? 0,
                            stageDescription = stage?.Description,
                            objectives = a.Objectives.Select(o =>
                            {
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

            getHint = new Func<string, string, string?>((entityIdStr, questId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return null;
                }

                var state = _questService.GetState(id);
                var active = state?.Active.FirstOrDefault(a => a.QuestId == questId);
                if (active == null)
                {
                    return null;
                }

                var def = _questRegistry.Get(questId);
                var stage = def?.Stages?.ElementAtOrDefault(active.StageIndex);
                return stage?.Hint;
            }),

            getActiveHints = new Func<string, string[]>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return [];
                }

                var state = _questService.GetState(id);
                if (state == null)
                {
                    return [];
                }

                var hints = new List<string>();
                foreach (var active in state.Active)
                {
                    var def = _questRegistry.Get(active.QuestId);
                    var stage = def?.Stages?.ElementAtOrDefault(active.StageIndex);
                    if (stage?.Hint != null)
                    {
                        hints.Add(stage.Hint);
                    }
                }

                return [.. hints];
            }),

            hasQuestMarker = new Func<string, string, bool>((entityIdStr, templateId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id))
                {
                    return false;
                }

                return _questMarkerService.HasQuestMarker(id, templateId);
            }),

            registerObjectiveType = new Action<string, object>((type, handler) =>
            {
                // Placeholder for custom objective type registration.
                // Full integration requires QuestObjectiveWatcher wiring; no-op for now.
            }),

            registerScript = new Action<string, JsValue>((questId, hooksObj) =>
            {
                if (_scriptLoader.JintEngine == null)
                {
                    _scriptLoader.JintEngine = engine;
                }

                _scriptLoader.Register(questId, hooksObj);
            }),
        };
    }
}
