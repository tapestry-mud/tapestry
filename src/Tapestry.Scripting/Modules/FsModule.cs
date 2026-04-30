using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class FsModule : IJintApiModule
{
    private readonly string _serverRootPath;

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public FsModule(string serverRootPath)
    {
        _serverRootPath = serverRootPath;
    }

    public string Namespace => "fs";

    public object Build(JintEngine jint)
    {
        return new
        {
            writeYaml = new Action<string, JsValue>((relativePath, data) =>
            {
                ValidatePath(relativePath);
                var connectionsDir = Path.Combine(_serverRootPath, "connections");
                Directory.CreateDirectory(connectionsDir);
                var filePath = Path.Combine(connectionsDir, relativePath);

                var obj = ConvertJsValueToObject(data);
                var yaml = Serializer.Serialize(obj);
                File.WriteAllText(filePath, yaml);
            }),

            deleteFile = new Action<string>(relativePath =>
            {
                ValidatePath(relativePath);
                var connectionsDir = Path.Combine(_serverRootPath, "connections");
                var filePath = Path.Combine(connectionsDir, relativePath);

                if (File.Exists(filePath)) { File.Delete(filePath); }
            })
        };
    }

    private void ValidatePath(string relativePath)
    {
        var connectionsDir = Path.GetFullPath(Path.Combine(_serverRootPath, "connections"));
        var fullPath = Path.GetFullPath(Path.Combine(connectionsDir, relativePath));

        if (!fullPath.StartsWith(connectionsDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path escapes connections directory.");
        }
    }

    private static object ConvertJsValueToObject(JsValue value)
    {
        if (value.Type == Types.Undefined || value.Type == Types.Null)
        {
            return null!;
        }

        if (value.Type == Types.String)
        {
            return value.ToString();
        }

        if (value.Type == Types.Number)
        {
            var numVal = (double)value.ToObject()!;
            if (numVal == Math.Floor(numVal))
            {
                return (int)numVal;
            }
            return numVal;
        }

        if (value.Type == Types.Boolean)
        {
            return (bool)value.ToObject()!;
        }

        if (value is JsArray jsArray)
        {
            var list = new List<object?>();
            for (uint i = 0; i < jsArray.Length; i++)
            {
                var elem = jsArray[(int)i];
                list.Add(ConvertJsValueToObject(elem));
            }
            return list;
        }

        if (value is ObjectInstance objInstance)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in objInstance.GetOwnProperties())
            {
                var propValue = prop.Value.Value;
                dict[prop.Key.ToString()] = ConvertJsValueToObject(propValue);
            }
            return dict;
        }

        return value.ToString();
    }
}
