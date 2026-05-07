using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine.Persistence;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Server.Persistence;

public class FilePlayerStore : IPlayerStore
{
    private readonly string _playersDir;
    private readonly ILogger<FilePlayerStore> _logger;

    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;

    public FilePlayerStore(ServerConfig config, ILogger<FilePlayerStore> logger)
    {
        var savePath = config.Persistence.SavePath;
        if (!Path.IsPathRooted(savePath))
        {
            savePath = Path.GetFullPath(savePath, config.ConfigDirectory);
        }
        _playersDir = Path.Combine(savePath, "players");
        _logger = logger;

        Directory.CreateDirectory(_playersDir);

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithQuotingNecessaryStrings()
            .Build();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public bool Exists(string playerName)
    {
        return File.Exists(GetFilePath(playerName));
    }

    public async Task<PlayerSaveData?> LoadAsync(string playerName)
    {
        var path = GetFilePath(playerName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var yaml = await File.ReadAllTextAsync(path);
            var data = _yamlDeserializer.Deserialize<PlayerSaveData>(yaml);
            if (data == null)
            {
                return null;
            }

            if (data.Version < SaveMigrations.CurrentVersion)
            {
                _logger.LogInformation("Player save {Name} at version {Old}, current is {Current}",
                    playerName, data.Version, SaveMigrations.CurrentVersion);
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load player save: {Name}", playerName);
            return null;
        }
    }

    public async Task SaveAsync(PlayerSaveData data)
    {
        if (string.IsNullOrEmpty(data.PasswordHash))
        {
            throw new InvalidOperationException(
                $"Refusing to save player '{data.Name}' with null or empty password hash.");
        }

        var path = GetFilePath(data.Name);
        var tmpPath = path + ".tmp";
        var bakPath = path + ".bak";

        var yaml = _yamlSerializer.Serialize(data);

        await File.WriteAllTextAsync(tmpPath, yaml);

        if (File.Exists(path))
        {
            File.Move(path, bakPath, overwrite: true);
        }

        File.Move(tmpPath, path);

        if (File.Exists(bakPath))
        {
            File.Delete(bakPath);
        }
    }

    public Task DeleteAsync(string playerName)
    {
        var path = GetFilePath(playerName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private string GetFilePath(string playerName)
    {
        var resolved = Path.GetFullPath(Path.Combine(_playersDir, playerName.ToLowerInvariant() + ".yaml"));
        var safeBase = Path.GetFullPath(_playersDir);
        if (!resolved.StartsWith(safeBase + Path.DirectorySeparatorChar) && resolved != safeBase)
        {
            throw new ArgumentException($"Path traversal detected for player name: {playerName}");
        }
        return resolved;
    }
}
