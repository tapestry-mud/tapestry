namespace Tapestry.Engine.Distribution;

/// <summary>
/// Matches entities and rooms against a SelectorSpec, and resolves the full set
/// of matching entities from the world (for use in schedule everyForEach).
/// </summary>
public static class EntitySelector
{
    /// <summary>True if this live entity matches the selector.</summary>
    public static bool MatchesEntity(Entity entity, SelectorSpec spec)
    {
        if (spec.Shop) { return false; }
        if (spec.Id != null)
        {
            return string.Equals(
                entity.GetProperty<string>(CommonProperties.TemplateId),
                spec.Id,
                StringComparison.OrdinalIgnoreCase);
        }
        if (spec.Type != null)
        {
            return string.Equals(entity.Type, spec.Type, StringComparison.OrdinalIgnoreCase);
        }
        if (spec.Tag != null)
        {
            return entity.HasTag(spec.Tag);
        }
        return false;
    }

    /// <summary>True if this room matches the selector.</summary>
    public static bool MatchesRoom(Room room, SelectorSpec spec)
    {
        if (spec.Shop) { return false; }
        if (spec.Id != null)
        {
            return string.Equals(room.Id, spec.Id, StringComparison.OrdinalIgnoreCase);
        }
        if (spec.Type != null)
        {
            return string.Equals(spec.Type, "room", StringComparison.OrdinalIgnoreCase);
        }
        if (spec.Tag != null)
        {
            return room.HasTag(spec.Tag);
        }
        return false;
    }

    /// <summary>
    /// Resolve the full set of entities matching the selector from the world.
    /// Used by ScheduleModule.everyForEach. Does NOT include rooms.
    /// </summary>
    public static IEnumerable<Entity> ResolveEntities(World world, SelectorSpec spec)
    {
        if (spec.Shop || (!spec.HasTargetingKey)) { return Enumerable.Empty<Entity>(); }
        if (spec.Id != null)
        {
            // Guid → direct entity lookup; otherwise → template-id scan
            if (Guid.TryParse(spec.Id, out var guid))
            {
                var e = world.GetEntity(guid);
                return e != null ? new[] { e } : Enumerable.Empty<Entity>();
            }
            return world.GetEntitiesByTemplateId(spec.Id);
        }
        if (spec.Type != null) { return world.GetEntitiesByType(spec.Type); }
        if (spec.Tag != null) { return world.GetEntitiesByTag(spec.Tag); }
        return Enumerable.Empty<Entity>();
    }
}
