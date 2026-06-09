namespace Tapestry.Engine.Watch;

/// <summary>
/// One watchable player in a watch-mode <c>roster</c> frame. The field names serialize (camelCase)
/// to the <c>entityId</c>/<c>name</c>/<c>roomId</c> the client viewer expects. MVP roster is
/// everyone online; the future <c>crawler</c>-tag filter narrows the source with zero rework.
/// </summary>
public sealed record WatchRosterEntry(string EntityId, string Name, string RoomId);
