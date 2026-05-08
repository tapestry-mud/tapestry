// src/Tapestry.Scripting/MobAbilityEntryConverter.cs
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Tapestry.Engine.Mobs;

public class MobAbilityEntryConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(MobAbilityEntry);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return new MobAbilityEntry { Id = scalar.Value };
        }

        parser.Consume<MappingStart>();
        var entry = new MobAbilityEntry();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            var val = parser.Consume<Scalar>().Value;
            if (key == "id")
            {
                entry.Id = val;
            }
            else if (key == "proficiency" && int.TryParse(val, out var p))
            {
                entry.Proficiency = p;
            }
        }
        return entry;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        throw new NotSupportedException();
    }
}
