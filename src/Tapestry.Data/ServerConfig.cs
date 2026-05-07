using Tapestry.Shared;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Data;

public class ServerConfig
{
    public ServerSection Server { get; set; } = new();
    public DatabaseSection Database { get; set; } = new();
    public List<string> Packs { get; set; } = new();
    public LlmSection Llm { get; set; } = new();
    public LoggingSection Logging { get; set; } = new();
    public TelemetrySection Telemetry { get; set; } = new();
    public PersistenceSection Persistence { get; set; } = new();
    public NetworkingSection Networking { get; set; } = new();
    public TrainingSection Training { get; set; } = new();
    public EconomySection Economy { get; set; } = new();
    public GameSection Game { get; set; } = new();
    public CombatSection Combat { get; set; } = new();
    public MsspConfig Mssp { get; set; } = new();
    public IdleSection Idle { get; set; } = new();

    public string ConfigDirectory { get; private set; } = "";

    public static ServerConfig Load(string path)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var baseYaml = File.ReadAllText(path);
        var baseDict = deserializer.Deserialize<Dictionary<object, object>>(baseYaml) ?? new();

        var localPath = Path.Combine(Path.GetDirectoryName(path)!, "server.local.yaml");
        if (File.Exists(localPath))
        {
            var localYaml = File.ReadAllText(localPath);
            var localDict = deserializer.Deserialize<Dictionary<object, object>>(localYaml);
            if (localDict != null)
            {
                DeepMerge(baseDict, localDict);
            }
        }

        var serializer = new YamlDotNet.Serialization.Serializer();
        var mergedYaml = serializer.Serialize(baseDict);
        var config = deserializer.Deserialize<ServerConfig>(mergedYaml);
        config.ConfigDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        return config;
    }

    private static void DeepMerge(Dictionary<object, object> target, Dictionary<object, object> overlay)
    {
        foreach (var (key, value) in overlay)
        {
            if (value is Dictionary<object, object> overlaySection
                && target.TryGetValue(key, out var existing)
                && existing is Dictionary<object, object> targetSection)
            {
                DeepMerge(targetSection, overlaySection);
            }
            else
            {
                target[key] = value;
            }
        }
    }
}

public class ServerSection
{
    public string Name { get; set; } = "Tapestry MUD";
    public string Motd { get; set; } = "Welcome to Tapestry!";
    public int TelnetPort { get; set; } = 4000;
    public int WebsocketPort { get; set; } = 4001;
    public int MaxConnections { get; set; } = 200;
    public int TickRateMs { get; set; } = 100;
}

public class DatabaseSection
{
    public string ConnectionString { get; set; } = "";
}

public class LlmSection
{
    public string Provider { get; set; } = "none";
    public string ApiKeyEnv { get; set; } = "";
    public bool BuilderEnabled { get; set; }
    public bool FlavorEnabled { get; set; }
}

public class LoggingSection
{
    public string Level { get; set; } = "Information";
}

public class TelemetrySection
{
    public bool Enabled { get; set; } = false;
    public string Endpoint { get; set; } = "http://localhost:4317";
    public string Protocol { get; set; } = "grpc";
    public string ServiceName { get; set; } = "tapestry";
    public TelemetryConsoleSection Console { get; set; } = new();
    public AdminChannelSection AdminChannel { get; set; } = new();
}

public class TelemetryConsoleSection
{
    public bool Enabled { get; set; } = true;
    public string Format { get; set; } = "text";
}

public class AdminChannelSection
{
    public int SlowTickThresholdMs { get; set; } = 50;
    public string Tag { get; set; } = "admin";
}

public class PersistenceSection
{
    public string SavePath { get; set; } = "./data/saves";
    public string ConnectionsPath { get; set; } = "./data/connections";
    public int AutosaveInterval { get; set; } = 300;
    public int PasswordMinLength { get; set; } = 6;
    public int MaxLoginAttempts { get; set; } = 5;
}

public class NetworkingSection
{
    public int NegotiationTimeoutMs { get; set; } = 500;
}

public class EconomySection
{
    public double ShopBuyMarkup { get; set; } = 1.2;
    public double ShopSellDiscount { get; set; } = 0.5;
}

public class TrainingSection
{
    public bool RequireSafeRoomForStats { get; set; } = false;
    public List<string> TrainableStats { get; set; } = new()
    {
        "strength", "intelligence", "wisdom", "dexterity", "constitution", "luck"
    };
    public int CatchUpBoost { get; set; } = 5;
}

public class PhaseTimeoutsSection
{
    public int Name { get; set; } = 0;
    public int Password { get; set; } = 0;
    public int SessionTakeover { get; set; } = 0;
    public int Creating { get; set; } = 0;
}

public class IdleSection
{
    public int WarnSeconds { get; set; } = 0;
    public int TimeoutSeconds { get; set; } = 0;
    public int PreLoginTimeoutSeconds { get; set; } = 120;
    public string WarnMessage { get; set; } = "The world grows distant... you are fading.";
    public string TimeoutMessage { get; set; } = "You have faded from the world.";
    public string AdminTag { get; set; } = "admin";
    public PhaseTimeoutsSection PhaseTimeouts { get; set; } = new();
}

public class GameSection
{
    public int TicksPerGameHour { get; set; } = 600;
    public int[] PeriodBoundaries { get; set; } = [5, 8, 18, 20]; // dawn, day, dusk, night
    public float DefaultOccupiedModifier { get; set; } = 3.0f;
    public int DefaultResetInterval { get; set; } = 3000;
    public int WeatherRollIntervalHours { get; set; } = 24;
}

public class CombatSection
{
    public double LuckScale { get; set; } = 0.002;
}
