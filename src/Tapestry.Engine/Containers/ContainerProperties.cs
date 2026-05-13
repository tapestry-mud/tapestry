using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Containers;

public static class ContainerProperties
{
    public const string Public = "public";
    public const string ContainerCapacity = "container_capacity";
    public const string ContainerWeightLimit = "container_weight_limit";
    public const string FillType = "fill_type";
    public const string FillSource = "fill_source";
    public const string FillSupply = "fill_supply";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(Public, "Whether this container is public", PropertyValueType.Bool, appliesTo: new[] { "item" });
        registry.RegisterEngineProperty(ContainerCapacity, "Maximum item count this container holds", PropertyValueType.Int, appliesTo: new[] { "item" });
        registry.RegisterEngineProperty(ContainerWeightLimit, "Maximum weight this container holds", PropertyValueType.Int, appliesTo: new[] { "item" });
        registry.RegisterEngineProperty(FillType, "Liquid type this container can hold", PropertyValueType.String, appliesTo: new[] { "item" });
        registry.RegisterEngineProperty(FillSource, "Source entity ID for fill refills", PropertyValueType.String, appliesTo: new[] { "item" });
        registry.RegisterEngineProperty(FillSupply, "Current fill supply amount", PropertyValueType.Int, appliesTo: new[] { "item" });
    }
}
