using JintEngine = Jint.Engine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Scripting.Modules;

public class DataModule : IJintApiModule
{
    private readonly PackContext _packContext;
    private static readonly IDeserializer _yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public DataModule(PackContext packContext)
    {
        _packContext = packContext;
    }

    public string Namespace => "data";

    public object Build(JintEngine engine)
    {
        return new
        {
            loadYaml = new Func<string, object?>(LoadYaml)
        };
    }

    private object? LoadYaml(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_packContext.CurrentPackDir))
        {
            return null;
        }

        var packDir = Path.GetFullPath(_packContext.CurrentPackDir);
        var fullPath = Path.GetFullPath(Path.Combine(packDir, relativePath));

        if (!fullPath.StartsWith(packDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.Equals(packDir, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!File.Exists(fullPath))
        {
            return null;
        }

        var content = File.ReadAllText(fullPath);
        return _yaml.Deserialize<object>(content);
    }
}
