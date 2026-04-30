using Tapestry.Engine.Persistence;

namespace Tapestry.Engine;

/// <summary>
/// Shared entity property keys used across multiple engine modules.
/// </summary>
public static class CommonProperties
{
    /// <summary>Key: "template_id"</summary>
    public const string TemplateId = "template_id";

    /// <summary>Key: "regen_hp"</summary>
    public const string RegenHp = "regen_hp";

    /// <summary>Key: "regen_resource"</summary>
    public const string RegenResource = "regen_resource";

    /// <summary>Key: "regen_movement"</summary>
    public const string RegenMovement = "regen_movement";

    /// <summary>Key: "corpse_decay"</summary>
    public const string CorpseDecay = "corpse_decay";

    /// <summary>Key: "corpse_created_tick"</summary>
    public const string CorpseCreatedTick = "corpse_created_tick";

    /// <summary>Key: "class"</summary>
    public const string Class = "class";

    /// <summary>Key: "race"</summary>
    public const string Race = "race";

    /// <summary>Key: "alignment"</summary>
    public const string Alignment = "alignment";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(TemplateId, typeof(string));
        registry.Register(RegenHp, typeof(int));
        registry.Register(RegenResource, typeof(int));
        registry.Register(RegenMovement, typeof(int));
        registry.Register(CorpseDecay, typeof(int));
        registry.Register(CorpseCreatedTick, typeof(long));
        registry.Register(Class, typeof(string));
        registry.Register(Race, typeof(string));
        registry.Register(Alignment, typeof(int));

        registry.RegisterTransient("alignment_history");
        registry.RegisterTransient("no_follow");
        registry.RegisterTransient("following");
        registry.RegisterTransient("group_id");
        registry.RegisterTransient("group_leader");
        registry.RegisterTransient("group_join_time");
        registry.RegisterTransient("group_invite_from");
        registry.RegisterTransient("group_invite_expires");
    }
}
