using Tapestry.Engine.Tags;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Scripting.Tags;

public static class TagsFileLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static void LoadIntoRegistry(string packDirectory, string packName, TagRegistry registry)
    {
        var path = Path.Combine(packDirectory, "tags.yml");
        if (!File.Exists(path)) { return; }

        var yaml = File.ReadAllText(path);
        var model = Deserializer.Deserialize<TagsFileModel>(yaml) ?? new TagsFileModel();
        var isEngine = packName == "tapestry-core";

        foreach (var (name, entry) in model.Tags)
        {
            if (isEngine)
            {
                registry.RegisterEngineTag(name, entry.Description, entry.AppliesTo);
            }
            else
            {
                registry.RegisterPackTag(packName, name, entry.Description, entry.AppliesTo);
            }
        }
    }
}
