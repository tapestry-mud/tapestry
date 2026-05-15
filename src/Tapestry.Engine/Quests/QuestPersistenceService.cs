using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Engine.Quests;

public class QuestPersistenceService : IQuestPersistence
{
    private readonly string _playersDir;
    private readonly QuestRegistry _registry;
    private readonly EventBus _eventBus;
    private readonly QuestStateRepository _repository;
    private readonly ILogger<QuestPersistenceService> _logger;
    private bool _started;

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public QuestPersistenceService(
        ServerConfig config,
        QuestRegistry registry,
        EventBus eventBus,
        QuestStateRepository repository,
        ILogger<QuestPersistenceService>? logger = null)
    {
        _registry = registry;
        _eventBus = eventBus;
        _repository = repository;
        _logger = logger ?? NullLogger<QuestPersistenceService>.Instance;

        var savePath = config.Persistence.SavePath;
        if (!Path.IsPathRooted(savePath))
        {
            savePath = Path.GetFullPath(savePath, config.ConfigDirectory);
        }
        _playersDir = Path.Combine(savePath, "players");
    }

    public void Start()
    {
        if (_started) { return; }
        _started = true;

        _eventBus.Subscribe("player.login", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            if (!evt.Data.TryGetValue("playerName", out var nameObj)) { return; }
            var playerName = nameObj?.ToString();
            if (string.IsNullOrEmpty(playerName)) { return; }

            var state = Load(playerName);
            if (state != null)
            {
                _repository.Set(evt.SourceEntityId.Value, state);
            }
        });
    }

    public void Save(string playerName, QuestState state)
    {
        var path = GetPath(playerName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var yaml = Serializer.Serialize(state);
        File.WriteAllText(path, yaml);
    }

    public QuestState? Load(string playerName)
    {
        var path = GetPath(playerName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(path);
            var state = Deserializer.Deserialize<QuestState>(yaml);
            return RemoveOrphans(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load quest state for {Player}", playerName);
            return null;
        }
    }

    private QuestState RemoveOrphans(QuestState state)
    {
        // Only filter orphans when the registry has been populated.
        // An empty registry means no packs have been loaded yet -- don't wipe all quests.
        if (_registry.All().Count == 0)
        {
            return state;
        }

        state.Active.RemoveAll(a => _registry.Get(a.QuestId) == null);
        state.Completed.RemoveAll(id => _registry.Get(id) == null);
        return state;
    }

    private string GetPath(string playerName) =>
        Path.Combine(_playersDir, $"{playerName.ToLowerInvariant()}.quests.yaml");
}
