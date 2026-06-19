using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Quests;
using Tapestry.Engine.Registration;

using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

internal sealed class QuestScriptHooks
{
    public string Pack { get; init; } = "";
    public JsValue? OnGranted { get; init; }
    public JsValue? OnObjectiveAdvanced { get; init; }
    public JsValue? OnCompleted { get; init; }
    public JsValue? OnStageAdvanced { get; init; }
}

public class QuestScriptLoader : IQuestScriptLoader
{
    private readonly Dictionary<string, QuestScriptHooks> _scripts = new();
    private readonly World _world;
    private readonly ILogger<QuestScriptLoader> _logger;
    private readonly RegistrationGate? _gate;

    internal JintEngine? JintEngine { get; set; }

    public QuestScriptLoader(World world, ILogger<QuestScriptLoader> logger, RegistrationGate? gate = null)
    {
        _world = world;
        _logger = logger;
        _gate = gate;
    }

    /// <summary>
    /// Plain write (upsert). Collision resolution is the RegistrationPolicy's job — by the
    /// time a write lands here, the policy has already elected the winner.
    /// </summary>
    public void Register(string questId, JsValue hooksObj, string pack = "")
    {
        _gate?.AssertCommitScope("quest-hook", questId);
        if (hooksObj is not ObjectInstance obj)
        {
            return;
        }

        var hooks = new QuestScriptHooks
        {
            Pack = pack,
            OnGranted = GetFn(obj, "onGranted"),
            OnObjectiveAdvanced = GetFn(obj, "onObjectiveAdvanced"),
            OnCompleted = GetFn(obj, "onCompleted"),
            OnStageAdvanced = GetFn(obj, "onStageAdvanced"),
        };

        _scripts[questId] = hooks;
    }

    public bool HasScript(string questId)
    {
        return _scripts.ContainsKey(questId);
    }

    /// <summary>
    /// Test/diagnostic accessor: the pack owner recorded for a quest's hooks. Returns null
    /// only when no hooks are registered for the quest; returns the recorded owner otherwise,
    /// which may be the empty string if the hooks were registered with no owner (the blank-owner
    /// case this accessor exists to surface). Do not collapse empty-to-null -- that would hide it.
    /// </summary>
    internal string? OwnerOf(string questId)
    {
        return _scripts.TryGetValue(questId, out var hooks) ? hooks.Pack : null;
    }

    public bool CallOnGranted(string questId, Guid playerId)
    {
        if (!_scripts.TryGetValue(questId, out var hooks) || hooks.OnGranted == null || JintEngine == null)
        {
            return false;
        }

        var playerObj = BuildPlayerObj(playerId);
        if (playerObj == null)
        {
            return false;
        }

        try
        {
            var result = JintEngine.Invoke(hooks.OnGranted, JsValue.FromObject(JintEngine, playerObj));
            return result.Type == Types.Boolean && (bool)result.ToObject()!;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quest script onGranted threw for {QuestId}", questId);
            return false;
        }
    }

    public void CallOnObjectiveAdvanced(string questId, Guid playerId, string objectiveId, int current, int required)
    {
        if (!_scripts.TryGetValue(questId, out var hooks) || hooks.OnObjectiveAdvanced == null || JintEngine == null)
        {
            return;
        }

        var playerObj = BuildPlayerObj(playerId);
        if (playerObj == null)
        {
            return;
        }

        var argsObj = new { objectiveId, current, required };
        try
        {
            JintEngine.Invoke(hooks.OnObjectiveAdvanced, JsValue.FromObject(JintEngine, playerObj), JsValue.FromObject(JintEngine, argsObj));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quest script onObjectiveAdvanced threw for {QuestId}", questId);
        }
    }

    public void CallOnCompleted(string questId, Guid playerId)
    {
        if (!_scripts.TryGetValue(questId, out var hooks) || hooks.OnCompleted == null || JintEngine == null)
        {
            return;
        }

        var playerObj = BuildPlayerObj(playerId);
        if (playerObj == null)
        {
            return;
        }

        try
        {
            JintEngine.Invoke(hooks.OnCompleted, JsValue.FromObject(JintEngine, playerObj));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quest script onCompleted threw for {QuestId}", questId);
        }
    }

    public void CallOnStageAdvanced(string questId, Guid playerId, int stageIndex)
    {
        if (!_scripts.TryGetValue(questId, out var hooks) || hooks.OnStageAdvanced == null || JintEngine == null)
        {
            return;
        }

        var playerObj = BuildPlayerObj(playerId);
        if (playerObj == null)
        {
            return;
        }

        var argsObj = new { stageIndex };
        try
        {
            JintEngine.Invoke(hooks.OnStageAdvanced, JsValue.FromObject(JintEngine, playerObj), JsValue.FromObject(JintEngine, argsObj));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quest script onStageAdvanced threw for {QuestId}", questId);
        }
    }

    private object? BuildPlayerObj(Guid playerId)
    {
        var entity = _world.GetEntity(playerId);
        if (entity == null)
        {
            return null;
        }

        return new { entityId = entity.Id.ToString(), name = entity.Name };
    }

    private static JsValue? GetFn(ObjectInstance obj, string name)
    {
        var val = obj.Get(name);
        if (val == null || val.Type == Types.Undefined || val.Type == Types.Null)
        {
            return null;
        }

        return val;
    }
}
