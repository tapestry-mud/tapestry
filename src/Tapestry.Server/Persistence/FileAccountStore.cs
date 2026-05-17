using Microsoft.Extensions.Logging;
using Tapestry.Data;
using Tapestry.Engine.Persistence;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Server.Persistence;

public class FileAccountStore : IAccountStore
{
    private readonly string _accountsDir;
    private readonly string _indexPath;
    private readonly ILogger<FileAccountStore> _logger;

    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;

    private readonly Dictionary<string, Guid> _emailIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _indexLock = new();

    public FileAccountStore(ServerConfig config, ILogger<FileAccountStore> logger)
    {
        var savePath = config.Persistence.SavePath;
        if (!Path.IsPathRooted(savePath))
        {
            savePath = Path.GetFullPath(savePath, config.ConfigDirectory);
        }
        _accountsDir = Path.Combine(savePath, "accounts");
        _indexPath = Path.Combine(_accountsDir, "index.yaml");
        _logger = logger;

        Directory.CreateDirectory(_accountsDir);

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithQuotingNecessaryStrings()
            .Build();

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        LoadIndex();
    }

    private void LoadIndex()
    {
        if (!File.Exists(_indexPath))
        {
            return;
        }

        try
        {
            var yaml = File.ReadAllText(_indexPath);
            var dict = _yamlDeserializer.Deserialize<Dictionary<string, string>>(yaml);
            if (dict != null)
            {
                foreach (var (email, idStr) in dict)
                {
                    if (Guid.TryParse(idStr, out var id))
                    {
                        _emailIndex[email] = id;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load account email index");
        }
    }

    private void SaveIndex()
    {
        var dict = new Dictionary<string, string>();
        lock (_indexLock)
        {
            foreach (var (email, id) in _emailIndex)
            {
                dict[email] = id.ToString();
            }
        }

        Directory.CreateDirectory(_accountsDir);
        var yaml = _yamlSerializer.Serialize(dict);
        var tmpPath = _indexPath + ".tmp";
        File.WriteAllText(tmpPath, yaml);
        File.Move(tmpPath, _indexPath, overwrite: true);
    }

    public async Task<AccountSaveData?> LoadByIdAsync(Guid accountId)
    {
        var path = GetAccountFilePath(accountId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var yaml = await File.ReadAllTextAsync(path);
            return _yamlDeserializer.Deserialize<AccountSaveData>(yaml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load account {Id}", accountId);
            return null;
        }
    }

    public async Task<AccountSaveData?> LoadByEmailAsync(string email)
    {
        Guid accountId;
        lock (_indexLock)
        {
            if (!_emailIndex.TryGetValue(email.ToLowerInvariant(), out accountId))
            {
                return null;
            }
        }

        return await LoadByIdAsync(accountId);
    }

    public async Task SaveAsync(AccountSaveData data)
    {
        var dir = GetAccountDir(data.Id);
        Directory.CreateDirectory(dir);

        var path = GetAccountFilePath(data.Id);
        var tmpPath = path + ".tmp";
        var yaml = _yamlSerializer.Serialize(data);
        await File.WriteAllTextAsync(tmpPath, yaml);
        File.Move(tmpPath, path, overwrite: true);

        lock (_indexLock)
        {
            _emailIndex[data.Email.ToLowerInvariant()] = data.Id;
        }
        SaveIndex();
    }

    public Task DeleteAsync(Guid accountId)
    {
        var dir = GetAccountDir(accountId);

        lock (_indexLock)
        {
            var email = _emailIndex.FirstOrDefault(kv => kv.Value == accountId).Key;
            if (email != null)
            {
                _emailIndex.Remove(email);
            }
        }
        SaveIndex();

        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        return Task.CompletedTask;
    }

    public bool ExistsByEmail(string email)
    {
        lock (_indexLock)
        {
            return _emailIndex.ContainsKey(email.ToLowerInvariant());
        }
    }

    private string GetAccountDir(Guid accountId)
    {
        return Path.Combine(_accountsDir, accountId.ToString());
    }

    private string GetAccountFilePath(Guid accountId)
    {
        return Path.Combine(_accountsDir, accountId.ToString(), "account.yaml");
    }
}
