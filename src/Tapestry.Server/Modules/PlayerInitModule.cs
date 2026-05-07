using Microsoft.Extensions.Logging;
using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Races;
using Tapestry.Scripting;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Server.Modules;

public class PlayerInitModule : IGameModule
{
    private readonly ServerConfig _config;
    private readonly PackLoader _packLoader;
    private readonly PlayerPersistenceService _persistence;
    private readonly RaceRegistry _raceRegistry;
    private readonly SpawnManager _spawns;
    private readonly ILogger<PlayerInitModule> _logger;

    public string Name => "PlayerInit";

    public PlayerInitModule(
        ServerConfig config,
        PackLoader packLoader,
        PlayerPersistenceService persistence,
        RaceRegistry raceRegistry,
        SpawnManager spawns,
        ILogger<PlayerInitModule> logger)
    {
        _config = config;
        _packLoader = packLoader;
        _persistence = persistence;
        _raceRegistry = raceRegistry;
        _spawns = spawns;
        _logger = logger;
    }

    public void Configure()
    {
        LoadSeedPlayers();
        RunInitialSpawns();
    }

    private void LoadSeedPlayers()
    {
        var packsDir = Path.Combine(AppContext.BaseDirectory, "packs");

        foreach (var packName in _config.Packs)
        {
            if (!_packLoader.LoadedPacks.Any(p => p.Name == packName))
            {
                continue;
            }

            var playersPath = Path.Combine(packsDir, packName, "players.yaml");
            if (!File.Exists(playersPath))
            {
                continue;
            }

            var yaml = File.ReadAllText(playersPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var seedData = deserializer.Deserialize<SeedPlayersFile>(yaml);
            if (seedData?.Players == null)
            {
                continue;
            }

            foreach (var seed in seedData.Players)
            {
                if (_persistence.PlayerSaveExists(seed.Name))
                {
                    continue;
                }

                var entity = new Entity("player", seed.Name);
                foreach (var tag in seed.Tags)
                {
                    entity.AddTag(tag);
                }
                entity.Stats.BaseStrength = seed.Stats.Strength;
                entity.Stats.BaseIntelligence = seed.Stats.Intelligence;
                entity.Stats.BaseWisdom = seed.Stats.Wisdom;
                entity.Stats.BaseDexterity = seed.Stats.Dexterity;
                entity.Stats.BaseConstitution = seed.Stats.Constitution;
                entity.Stats.BaseLuck = seed.Stats.Luck;
                entity.Stats.BaseMaxHp = seed.Stats.MaxHp;
                entity.Stats.BaseMaxResource = seed.Stats.MaxResource;
                entity.Stats.BaseMaxMovement = seed.Stats.MaxMovement;
                entity.Stats.Hp = seed.Stats.MaxHp;
                entity.Stats.Resource = seed.Stats.MaxResource;
                entity.Stats.Movement = seed.Stats.MaxMovement;
                entity.SetProperty(CommonProperties.RegenHp, 2);
                entity.SetProperty(CommonProperties.RegenResource, 1);
                entity.SetProperty(CommonProperties.RegenMovement, 3);

                if (!string.IsNullOrEmpty(seed.PlayerClass))
                {
                    entity.SetProperty(CommonProperties.Class, seed.PlayerClass);
                }

                if (!string.IsNullOrEmpty(seed.PlayerRace))
                {
                    entity.SetProperty(CommonProperties.Race, seed.PlayerRace);
                    var raceDef = _raceRegistry.Get(seed.PlayerRace);
                    if (raceDef != null)
                    {
                        foreach (var flag in raceDef.RacialFlags)
                        {
                            entity.AddTag(flag);
                        }
                    }
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(seed.Password);
                _persistence.SaveNewPlayer(entity, hash).GetAwaiter().GetResult();

                _logger.LogInformation("Created seed player: {Name}", seed.Name);
            }
        }
    }

    private void RunInitialSpawns()
    {
        foreach (var areaName in _spawns.GetAreaNames())
        {
            _spawns.RunAreaReset(areaName);
        }
    }

    private class SeedPlayersFile
    {
        public List<SeedPlayer> Players { get; set; } = new();
    }

    private class SeedPlayer
    {
        public string Name { get; set; } = "";
        public string Password { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public string? PlayerClass { get; set; }
        public string? PlayerRace { get; set; }
        public SeedPlayerStats Stats { get; set; } = new();
    }

    private class SeedPlayerStats
    {
        public int Strength { get; set; } = 10;
        public int Intelligence { get; set; } = 10;
        public int Wisdom { get; set; } = 10;
        public int Dexterity { get; set; } = 10;
        public int Constitution { get; set; } = 10;
        public int Luck { get; set; } = 10;
        public int MaxHp { get; set; } = 100;
        public int MaxResource { get; set; } = 50;
        public int MaxMovement { get; set; } = 100;
    }
}
