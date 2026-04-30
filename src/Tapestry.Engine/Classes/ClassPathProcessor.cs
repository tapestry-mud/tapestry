using Microsoft.Extensions.Logging;
using Tapestry.Engine.Abilities;
using Tapestry.Shared;

namespace Tapestry.Engine.Classes;

public class ClassPathProcessor
{
    private readonly World _world;
    private readonly ClassRegistry _classRegistry;
    private readonly AbilityRegistry _abilityRegistry;
    private readonly ProficiencyManager _proficiencyManager;
    private readonly SessionManager _sessions;
    private readonly ILogger<ClassPathProcessor> _logger;

    public ClassPathProcessor(
        World world,
        ClassRegistry classRegistry,
        AbilityRegistry abilityRegistry,
        ProficiencyManager proficiencyManager,
        SessionManager sessions,
        EventBus eventBus,
        ILogger<ClassPathProcessor> logger)
    {
        _world = world;
        _classRegistry = classRegistry;
        _abilityRegistry = abilityRegistry;
        _proficiencyManager = proficiencyManager;
        _sessions = sessions;
        _logger = logger;
        eventBus.Subscribe("progression.level.up", OnLevelUp);
        eventBus.Subscribe("character.created", OnCharacterCreated);
    }

    private void OnLevelUp(GameEvent evt)
    {
        if (!evt.SourceEntityId.HasValue) { return; }
        var entityId = evt.SourceEntityId.Value;

        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }

        var classId = entity.GetProperty<string>("class");
        if (string.IsNullOrEmpty(classId)) { return; }

        var classDef = _classRegistry.Get(classId);
        if (classDef == null || string.IsNullOrEmpty(classDef.Track)) { return; }

        var trackName = evt.Data.TryGetValue("track", out var t) ? t?.ToString() ?? "" : "";
        if (!string.Equals(trackName, classDef.Track, StringComparison.OrdinalIgnoreCase)) { return; }

        var newLevelRaw = evt.Data.TryGetValue("newLevel", out var nl) ? nl : null;
        var newLevel = newLevelRaw is int i ? i : newLevelRaw is double d ? (int)d : 0;

        GrantPathEntries(entity, classDef, newLevel);
    }

    private void OnCharacterCreated(GameEvent evt)
    {
        if (!evt.SourceEntityId.HasValue) { return; }
        var entity = _world.GetEntity(evt.SourceEntityId.Value);
        if (entity == null) { return; }

        var classId = entity.GetProperty<string>("class");
        if (string.IsNullOrEmpty(classId)) { return; }

        var classDef = _classRegistry.Get(classId);
        if (classDef == null) { return; }

        GrantPathEntries(entity, classDef, 1);
    }

    private void GrantPathEntries(Entity entity, ClassDefinition classDef, int level)
    {
        foreach (var entry in classDef.Path)
        {
            if (entry.Level != level) { continue; }
            if (!string.IsNullOrEmpty(entry.UnlockedVia)) { continue; }

            var abilityDef = _abilityRegistry.Get(entry.AbilityId);
            if (abilityDef == null)
            {
                _logger.LogWarning(
                    "ClassPathProcessor: unknown ability '{AbilityId}' on class '{ClassId}' at level {Level}",
                    entry.AbilityId, classDef.Id, entry.Level);
                continue;
            }

            _proficiencyManager.Learn(entity.Id, entry.AbilityId);
            _sessions.GetByEntityId(entity.Id)?.Send($"You have learned {abilityDef.Name}!\r\n");
        }
    }
}
