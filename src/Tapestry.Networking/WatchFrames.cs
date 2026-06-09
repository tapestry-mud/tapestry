using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tapestry.Networking;

/// <summary>
/// Serializes the watch-mode typed envelopes the anonymous spectator transport speaks. The stock
/// <see cref="WebSocketConnection"/> only emits <c>text</c>/<c>gmcp</c> frames at the raw transport
/// (which sits BELOW the watch tap point), so the watch sink serializes its OWN frames here:
/// <c>watch</c> (tee'd player output), <c>roster</c> (watchable players), and <c>status</c>
/// (a line). The client demuxes on <c>type</c> exactly as it already does for <c>text</c>/<c>gmcp</c>
/// — "free on the client demux, small-but-real work on the engine sink".
/// </summary>
public static class WatchFrames
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Watch(string data) =>
        JsonSerializer.Serialize(new { type = "watch", data }, Options);

    public static string Roster(object roster) =>
        JsonSerializer.Serialize(new { type = "roster", data = roster }, Options);

    public static string Status(string message) =>
        JsonSerializer.Serialize(new { type = "status", data = message }, Options);
}
