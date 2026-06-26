namespace Tapestry.Engine.Consequence;

/// <summary>A single consequence stamped on a room: an opaque content-defined
/// <paramref name="Kind"/> (e.g. "looted", "collapsed") and its eviction
/// <paramref name="Lifespan"/> ("ephemeral" | "persistent" | "succession-seed").
/// The engine treats both as opaque strings - it never reasons about content meaning.</summary>
public sealed record ConsequenceEntry(string Kind, string Lifespan);
