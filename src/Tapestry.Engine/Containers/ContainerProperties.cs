// src/Tapestry.Engine/Containers/ContainerProperties.cs
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Containers;

public static class ContainerProperties
{
    // Container item properties
    public const string Fixed = "fixed";
    public const string Public = "public";
    public const string ContainerCapacity = "container_capacity";
    public const string ContainerWeightLimit = "container_weight_limit";

    // Fillable item properties
    public const string FillType = "fill_type";
    public const string FillSource = "fill_source";
    public const string FillSupply = "fill_supply";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(Fixed, typeof(bool));
        registry.Register(Public, typeof(bool));
        registry.Register(ContainerCapacity, typeof(int));
        registry.Register(ContainerWeightLimit, typeof(int));
        registry.Register(FillType, typeof(string));
        registry.Register(FillSource, typeof(string));
        registry.Register(FillSupply, typeof(int));
    }
}
