namespace Tapestry.Engine.Tags;

/// <summary>
/// Canonical declaration site for every tag the engine itself reads via
/// HasTag(...) string literals. Packs declare their own tags through
/// tags.yml (see TagsFileLoader); this class is the equivalent for the
/// engine, so a typo like HasTag("no_kil") fails loudly at boot instead
/// of silently doing nothing.
/// </summary>
public static class EngineTags
{
    public const string NoKill = "no_kill";
    public const string Safe = "safe";
    public const string NoFlee = "no_flee";
    public const string NoRegen = "no_regen";
    public const string Fixture = "fixture";
    public const string NoGet = "no_get";
    public const string Corpse = "corpse";
    public const string PlayerCorpse = "player_corpse";
    public const string EntryPoint = "entry_point";
    public const string SkillTrainer = "skill_trainer";
    public const string Linkdead = "linkdead";
    public const string FillSource = "fill_source";
    public const string Persistent = "persistent";
    public const string NoWander = "no_wander";

    public static void Register(TagRegistry registry)
    {
        registry.RegisterEngineTag(NoKill, "Prevents combat engagement", new[] { EntityTypes.Npc });
        registry.RegisterEngineTag(Safe, "Combat prohibited in this room", new[] { EntityTypes.Room });
        registry.RegisterEngineTag(NoFlee, "Prevents fleeing from combat with this mob", new[] { EntityTypes.Npc });
        registry.RegisterEngineTag(NoRegen, "Exempt from HP/resource/movement regeneration", new[] { EntityTypes.Player, EntityTypes.Npc });
        registry.RegisterEngineTag(Fixture, "Permanent room object, not removable", new[] { EntityTypes.Item });
        registry.RegisterEngineTag(NoGet, "Cannot be picked up from the ground", new[] { EntityTypes.Item });
        registry.RegisterEngineTag(Corpse, "Marks a container entity as a corpse", new[] { EntityTypes.Container });
        registry.RegisterEngineTag(PlayerCorpse, "Marks a corpse as belonging to a player", new[] { EntityTypes.Container });
        registry.RegisterEngineTag(EntryPoint, "Default entry room for new or returning players", new[] { EntityTypes.Room });
        registry.RegisterEngineTag(SkillTrainer, "NPC can teach abilities from TrainerConfig", new[] { EntityTypes.Npc });
        registry.RegisterEngineTag(Linkdead, "Player session has lost its connection and is pending timeout", new[] { EntityTypes.Player });
        registry.RegisterEngineTag(FillSource, "Provides liquid for filling fillable containers", new[] { EntityTypes.Item });
        registry.RegisterEngineTag(Persistent, "Respawns immediately on death or removal", new[] { EntityTypes.Npc });
        registry.RegisterEngineTag(NoWander, "Mobs will not wander or flee into this room", new[] { EntityTypes.Room });
    }
}
