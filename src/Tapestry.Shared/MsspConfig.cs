namespace Tapestry.Shared;

public class MsspConfig
{
    public string Name { get; set; } = "Tapestry MUD";
    public string Codebase { get; set; } = "Tapestry";
    public string Contact { get; set; } = "";
    public string Hostname { get; set; } = "";
    public int Port { get; set; } = 4000;
    public string Created { get; set; } = "2025";
    public string Language { get; set; } = "English";
    public string Family { get; set; } = "Custom";
    public List<string> Gameplay { get; set; } = new() { "Hack and Slash", "Roleplaying" };
    public bool Classes { get; set; } = true;
    public bool Races { get; set; } = true;
    public bool Levels { get; set; } = true;
    public bool Equipment { get; set; } = true;
    public bool Multiplaying { get; set; } = false;
    public bool PlayerKilling { get; set; } = true;
}

public record MsspDynamicValues
{
    public int Players { get; init; }
    public long UptimeEpoch { get; init; }
}
