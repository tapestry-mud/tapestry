namespace Tapestry.Engine;

/// <summary>
/// Shared property/tag-holder surface that both <see cref="Entity"/> and
/// <see cref="Room"/> implement, so <c>AttributeWriter</c> can write either.
/// </summary>
public interface IAttributeTarget
{
    /// <summary>Type discriminator used for registry <c>applies_to</c> checks
    /// (e.g. "player", "npc", "item", "room").</summary>
    string Type { get; }

    /// <summary>Human-readable name, used to compose user-facing write/describe
    /// result messages. Both <see cref="Entity"/> and <see cref="Room"/> expose this.</summary>
    string Name { get; }

    void SetProperty(string key, object? value);
    object? GetRawProperty(string key);

    void AddTag(string tag);
    void RemoveTag(string tag);
    bool HasTag(string tag);
}
