namespace Tapestry.Engine;

public class EmoteDefinition
{
    public required string Name { get; init; }
    public required string SelfMessage { get; init; }
    public required string RoomMessage { get; init; }
    public string? TargetMessage { get; init; }
    public string? TargetRoomMessage { get; init; }
}

public class EmoteRegistry
{
    private readonly Dictionary<string, EmoteDefinition> _emotes = new(StringComparer.OrdinalIgnoreCase);

    public void Register(EmoteDefinition emote)
    {
        { _emotes[emote.Name] = emote; }
    }

    public EmoteDefinition? Get(string name)
    {
        { return _emotes.GetValueOrDefault(name); }
    }

    public IEnumerable<string> AllEmotes => _emotes.Keys;

    public string Format(string template, string actorName, string? targetName = null)
    {
        {
            var result = template
                .Replace("{name}", actorName)
                .Replace("{possessive}", actorName + "'s");

            if (targetName != null)
            {
                { result = result.Replace("{target}", targetName); }
            }

            { return result; }
        }
    }
}
