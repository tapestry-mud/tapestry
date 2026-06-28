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
    public PreAuthSection PreAuth { get; set; } = new();
    public AccountsSection Accounts { get; set; } = new();
    public AdminSeedSection? Admin { get; set; }
    public FloodProtectionSection FloodProtection { get; set; } = new();
    public LinkDeadSection LinkDead { get; set; } = new();
    public OutputSection Output { get; set; } = new();
    public WatchSection Watch { get; set; } = new();
    public MobAiSection MobAi { get; set; } = new();

    public string ConfigDirectory { get; private set; } = "";

    /// <summary>
    /// Optional packs directory override, set from the <c>--packs</c> launch
    /// argument. Not read from server.yaml; null means "use the default".
    /// </summary>
    public string? PacksDirectory { get; set; }

    /// <summary>
    /// The effective packs directory: the <see cref="PacksDirectory"/> override
    /// when set, otherwise <c>packs/</c> beside the running binary.
    /// </summary>
    public string ResolvedPacksDirectory =>
        string.IsNullOrWhiteSpace(PacksDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "packs")
            : PacksDirectory;

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

public class OutputSection
{
    /// <summary>Default server-side word-wrap width (columns) for player output.
    /// 0 disables wrapping. A per-player <c>screen_width</c> overrides this; for telnet,
    /// a narrower NAWS-reported terminal width caps it further. Clamped to a sane range
    /// by OutputWidthResolver at send time.</summary>
    public int WrapWidth { get; set; } = 80;
}

public class ServerSection
{
    public string Name { get; set; } = "Tapestry MUD";
    public string? Motd { get; set; } = null;
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
    /// <summary>Off by default (the prod droplet can't reach a local GPU).</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Dev/demo only: bind the static stub when enabled:false.</summary>
    public bool UseStub { get; set; } = false;
    /// <summary>Opt-in: request structured JSON (response_format json_schema) when a pack supplies
    /// a schema. Default off so nothing changes without opt-in; a json_schema-capable provider
    /// (gpt-4o) sets this true. Providers that ignore it degrade to the pack's baked fallback.</summary>
    public bool StructuredOutput { get; set; } = false;
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string Model { get; set; } = "qwen2.5:7b";
    /// <summary>Name of the env var holding the API key. The key itself never lives in YAML.</summary>
    public string ApiKeyEnv { get; set; } = "TAPESTRY_LLM_API_KEY";
    /// <summary>True for OpenAI/Anthropic — provider reports IsEnabled:false if the env key is missing.</summary>
    public bool RequiresKey { get; set; } = false;
    public double Temperature { get; set; } = 0.8;
    public int MaxSentences { get; set; } = 2;
    public int Candidates { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public string SystemPrompt { get; set; } =
        "You are a terse MUD area author. Second person, present tense. No preamble, no 'Sure!'.";
    /// <summary>Optional override of the AREA authoring system prompt (region voice, distinct from
    /// the room SystemPrompt above). Empty = use the built-in DefaultAreaSystemPrompt.</summary>
    public string AreaSystemPrompt { get; set; } = "";
    /// <summary>Optional per-field task-line overrides; empty means use the built-in defaults.</summary>
    public Dictionary<string, string> TaskLines { get; set; } = new();
    /// <summary>Instruction appended after neighbor context: stay on-theme but make the room distinct. Empty = built-in default.</summary>
    public string NeighborGuidance { get; set; } = "";
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
    public string RoomsPath { get; set; } = "./data/areas";
    public int AutosaveInterval { get; set; } = 300;
    public int PasswordMinLength { get; set; } = 6;
    public int MaxLoginAttempts { get; set; } = 5;
}

public class NetworkingSection
{
    public int NegotiationTimeoutMs { get; set; } = 500;
    public List<string> TrustedProxies { get; set; } = new();
    public KeepAliveSection KeepAlive { get; set; } = new();
}

// Liveness detection for half-open connections (client vanished without a clean
// TCP FIN / WebSocket Close). Without this, a dropped connection's read loop blocks
// forever, the session never fires OnDisconnected, and the player zombies in the
// world holding a connection/concurrency slot. Telnet uses OS-level TCP keepalive;
// WebSocket uses ping/pong with an abort timeout. Detection window for both is
// roughly IdleSeconds + IntervalSeconds * RetryCount.
public class KeepAliveSection
{
    public bool Enabled { get; set; } = true;
    public int IdleSeconds { get; set; } = 60;
    public int IntervalSeconds { get; set; } = 15;
    public int RetryCount { get; set; } = 4;

    // Linux-only backstop (TCP_USER_TIMEOUT): max time an unacked write may stay outstanding
    // before the socket errors. Bounds how long a heartbeat write to a dead peer hangs. 0 disables.
    public int UserTimeoutSeconds { get; set; } = 30;

    // How often the game loop writes a liveness heartbeat to each PLAYING session's connection.
    // The heartbeat is what actually provokes the write that detects a half-open peer. 0 disables.
    public int HeartbeatSeconds { get; set; } = 20;
}

public class EconomySection
{
    public double ShopBuyMarkup { get; set; } = 1.2;
    public double ShopSellDiscount { get; set; } = 0.5;
}

// Anonymous web spectator transport (watch mode, Slice B). Off by default: a server that does not
// want watching leaves it disabled and the tokenless `?mode=watch` WebSocket entry is rejected, so
// the dormant tee primitive stays invisible. RosterIntervalTicks sets how often the watchable-player
// roster is re-pushed to each watcher (SessionManager fires no add/remove events, so the roster is
// poll-driven, not push-on-change). ~25 ticks is roughly 2.5s at a 100ms tick rate.
public class WatchSection
{
    public bool Enabled { get; set; } = false;
    public int RosterIntervalTicks { get; set; } = 25;
}

// Mob-AI loop bounding (2026-06-07 outage fix). Defaults grounded in prod data
// 2026-06-10 (~11us mean per-mob): the caps only engage on pathology, not on
// legitimate behaviors. tick_budget_ms bounds the whole sweep (deferred mobs
// resume next tick via cursor); invocation_cap_ms bounds one behavior call
// (Jint-interruptible since 4.9.3); quarantine_strikes disables a behavior
// after that many cap violations, until reboot.
public class MobAiSection
{
    public int TickBudgetMs { get; set; } = 25;
    public int InvocationCapMs { get; set; } = 50;
    public int QuarantineStrikes { get; set; } = 3;
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
    public int Email { get; set; } = 0;
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

    /// <summary>Ticks a combatant must wait after fleeing before it can flee again
    /// (and before it can re-engage). Makes a fleeing mob catchable. ~80 = 8s.</summary>
    public int FleeCooldownTicks { get; set; } = 80;

    /// <summary>Movement points spent on a successful flee.</summary>
    public int FleeMoveCost { get; set; } = 10;
}

public class AdminSeedSection
{
    public string Handle { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class FloodProtectionSection
{
    public float CommandsPerSecond { get; set; } = 15;
    public float BurstSize { get; set; } = 30;
    public int StrikeThreshold { get; set; } = 3;
    public int StrikeDecaySeconds { get; set; } = 10;
}

public class LinkDeadSection
{
    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 120;
}

public class AccountsSection
{
    public int MaxConcurrentCharacters { get; set; } = 1;
    public bool EmailVerificationRequired { get; set; } = false;
}
