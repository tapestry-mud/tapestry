using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Tapestry.Engine.Authoring;

/// <summary>Reads a scalar (legacy `down: id` -> {Target}) OR a mapping
/// ({stub:true,label:...} -> {Stub,Label}; {target:id} -> {Target}). Writes a non-stub as a
/// bare scalar (byte-identical to legacy, so pre-existing rooms never churn) and a stub as a
/// mapping. Mirrors MobAbilityEntryConverter, but implements WriteYaml for fidelity.</summary>
public sealed class ExitDataConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(ExitData);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return new ExitData { Target = scalar.Value };
        }
        parser.Consume<MappingStart>();
        var exit = new ExitData();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>().Value;
            var val = parser.Consume<Scalar>().Value;
            if (key == "stub") { exit.Stub = bool.TryParse(val, out var b) && b; }
            else if (key == "label") { exit.Label = val; }
            else if (key == "target") { exit.Target = val; }
        }
        return exit;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var exit = (ExitData)value!;
        if (!exit.Stub)
        {
            emitter.Emit(new Scalar(exit.Target ?? ""));
            return;
        }
        emitter.Emit(new MappingStart());
        emitter.Emit(new Scalar("stub"));
        emitter.Emit(new Scalar("true"));
        emitter.Emit(new Scalar("label"));
        emitter.Emit(new Scalar(exit.Label ?? ""));
        emitter.Emit(new MappingEnd());
    }
}
