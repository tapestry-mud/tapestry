using Tapestry.Engine.Persistence;

namespace Tapestry.Engine;

public static class CommonProperties
{
    public const string TemplateId = "template_id";
    public const string RegenHp = "regen_hp";
    public const string RegenResource = "regen_resource";
    public const string RegenMovement = "regen_movement";
    public const string CorpseDecay = "corpse_decay";
    public const string CorpseCreatedTick = "corpse_created_tick";
    public const string Class = "class";
    public const string Race = "race";
    public const string Alignment = "alignment";
    public const string Description = "description";
    public const string SourcePack = "source_pack";
    public const string LastTellFrom = "last_tell_from";
    public const string LastTellTo = "last_tell_to";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(TemplateId, "Template ID used to spawn this entity", PropertyValueType.String);
        registry.RegisterEngineProperty(RegenHp, "HP regeneration per tick", PropertyValueType.Int);
        registry.RegisterEngineProperty(RegenResource, "Resource regeneration per tick", PropertyValueType.Int);
        registry.RegisterEngineProperty(RegenMovement, "Movement regeneration per tick", PropertyValueType.Int);
        registry.RegisterEngineProperty(CorpseDecay, "Ticks until corpse decays", PropertyValueType.Int);
        registry.RegisterEngineProperty(CorpseCreatedTick, "World tick when corpse was created", PropertyValueType.Long);
        registry.RegisterEngineProperty(Class, "Character class", PropertyValueType.String);
        registry.RegisterEngineProperty(Race, "Character race", PropertyValueType.String);
        registry.RegisterEngineProperty(Alignment, "Alignment value (-1000 to 1000)", PropertyValueType.Int);
        registry.RegisterEngineProperty(Description, "Entity description text", PropertyValueType.String);
        registry.RegisterEngineProperty(SourcePack, "Pack that loaded this entity", PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty(LastTellFrom, "Last entity who sent a tell to this player", PropertyValueType.String, appliesTo: new[] { EntityTypes.Player });
        registry.RegisterEngineProperty(LastTellTo, "Last entity this player sent a tell to", PropertyValueType.String, appliesTo: new[] { EntityTypes.Player });

        registry.RegisterEngineProperty("alignment_history", "History of alignment shifts", PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty("no_follow", "Prevents entity from being followed", PropertyValueType.Bool, transient: true);
        registry.RegisterEngineProperty("following", "Entity this one is following", PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty("group_id", "Group membership ID", PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty("group_leader", "ID of group leader", PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty("group_join_time", "Tick when entity joined group", PropertyValueType.Long, transient: true);
        registry.RegisterEngineProperty("group_invite_from", "ID of entity who sent group invite", PropertyValueType.String, transient: true);
        registry.RegisterEngineProperty("group_invite_expires", "Tick when group invite expires", PropertyValueType.Long, transient: true);
    }
}
