using Tapestry.Engine.Persistence;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Scripting.Properties;

public static class PropertiesFileLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static void LoadIntoRegistry(string packDirectory, string packName, PropertyRegistry registry)
    {
        var path = Path.Combine(packDirectory, "properties.yml");
        if (!File.Exists(path))
        {
            return;
        }

        string yaml;
        using (var reader = new StreamReader(path, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            yaml = reader.ReadToEnd();
        }
        LoadFromYaml(packName, yaml, registry);
    }

    public static void LoadFromYaml(string packName, string yaml, PropertyRegistry registry)
    {
        var model = Deserializer.Deserialize<PropertiesFileModel>(yaml) ?? new PropertiesFileModel();
        foreach (var (name, entry) in model.Properties)
        {
            var valueType = ParseValueType(entry.Type);
            var appliesTo = entry.AppliesTo.Count > 0 ? entry.AppliesTo : null;
            registry.RegisterPackProperty(packName, name, entry.Description, valueType, appliesTo);
        }
    }

    private static PropertyValueType ParseValueType(string type) => type.ToLowerInvariant() switch
    {
        "string" => PropertyValueType.String,
        "int" => PropertyValueType.Int,
        "double" => PropertyValueType.Double,
        "bool" => PropertyValueType.Bool,
        "long" => PropertyValueType.Long,
        "map_int" => PropertyValueType.MapInt,
        "map_string" => PropertyValueType.MapString,
        "list_string" => PropertyValueType.ListString,
        _ => throw new InvalidOperationException($"Unknown property type '{type}'. Valid: string, int, double, bool, long, map_int, map_string, list_string")
    };
}
