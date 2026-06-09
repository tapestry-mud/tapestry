using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Tests.Persistence;

public class PropertyRegistryTests
{
    private readonly PropertyRegistry _registry = new();

    [Fact]
    public void EngineProperty_FullName_IsJustName()
    {
        _registry.RegisterEngineProperty("gold", "Currency", PropertyValueType.Int);
        _registry.TryResolve("gold", null, out var entry);
        Assert.Equal("gold", entry.FullName);
    }

    [Fact]
    public void PackProperty_FullName_IsScopedName()
    {
        _registry.RegisterPackProperty("my-pack", "loyalty_score", "Loyalty", PropertyValueType.Int);
        _registry.TryResolve("my-pack:loyalty_score", null, out var entry);
        Assert.Equal("my-pack:loyalty_score", entry.FullName);
    }

    [Fact]
    public void TryResolve_BareName_FindsEngineFirst()
    {
        _registry.RegisterEngineProperty("gold", "Currency", PropertyValueType.Int);
        Assert.True(_registry.TryResolve("gold", null, out var entry));
        Assert.Equal("engine", entry.Scope);
    }

    [Fact]
    public void TryResolve_BareName_FallsBackToCurrentPack()
    {
        _registry.RegisterPackProperty("my-pack", "loyalty_score", "Loyalty", PropertyValueType.Int);
        Assert.True(_registry.TryResolve("loyalty_score", "my-pack", out var entry));
        Assert.Equal("my-pack", entry.Scope);
    }

    [Fact]
    public void TryResolve_BareName_MissesOtherPack()
    {
        _registry.RegisterPackProperty("other-pack", "loyalty_score", "Loyalty", PropertyValueType.Int);
        Assert.False(_registry.TryResolve("loyalty_score", "my-pack", out _));
    }

    [Fact]
    public void RegisterPackProperty_CannotShadowEngineProperty()
    {
        _registry.RegisterEngineProperty("gold", "Currency", PropertyValueType.Int);
        Assert.Throws<InvalidOperationException>(() =>
            _registry.RegisterPackProperty("my-pack", "gold", "Copied gold", PropertyValueType.Int));
    }

    [Fact]
    public void RegisterEngineProperty_RejectsHyphenatedName()
    {
        Assert.Throws<ArgumentException>(() =>
            _registry.RegisterEngineProperty("my-property", "Bad name", PropertyValueType.String));
    }

    [Fact]
    public void RegisterEngineProperty_RejectsUpperCase()
    {
        Assert.Throws<ArgumentException>(() =>
            _registry.RegisterEngineProperty("MyProp", "Bad name", PropertyValueType.String));
    }

    [Fact]
    public void IsTransient_ReturnsTrueForTransientProperty()
    {
        _registry.RegisterEngineProperty("last_ip", "Login IP", PropertyValueType.String, transient: true);
        Assert.True(_registry.IsTransient("last_ip"));
    }

    [Fact]
    public void AppliesTo_Null_MatchesAllTypes()
    {
        _registry.RegisterEngineProperty("description", "Description", PropertyValueType.String);
        _registry.TryResolve("description", null, out var entry);
        Assert.True(entry.AppliesToType("player"));
        Assert.True(entry.AppliesToType("npc"));
        Assert.True(entry.AppliesToType("item"));
        Assert.True(entry.AppliesToType("room"));
    }

    [Fact]
    public void AppliesTo_Specified_RestrictsByType()
    {
        _registry.RegisterEngineProperty("terrain", "Terrain type", PropertyValueType.String,
            appliesTo: new[] { "room" });
        _registry.TryResolve("terrain", null, out var entry);
        Assert.True(entry.AppliesToType("room"));
        Assert.False(entry.AppliesToType("player"));
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        _registry.RegisterEngineProperty("gold", "Currency", PropertyValueType.Int);
        _registry.RegisterPackProperty("my-pack", "loyalty", "Loyalty", PropertyValueType.Int);
        Assert.Equal(2, _registry.GetAll().Count);
    }

    [Fact]
    public void GetValueType_ReturnsTypeForKnownProperty()
    {
        _registry.RegisterEngineProperty("gold", "Currency", PropertyValueType.Int);
        Assert.Equal(PropertyValueType.Int, _registry.GetValueType("gold"));
    }

    [Fact]
    public void GetValueType_ReturnsNullForUnknown()
    {
        Assert.Null(_registry.GetValueType("unknown_prop"));
    }

    [Fact]
    public void RegisterEngineProperty_CarriesConstraints_ThroughGetAll()
    {
        _registry.RegisterEngineProperty("hunger", "Hunger meter", PropertyValueType.Int,
            appliesTo: new[] { "player" }, min: 0, max: 100);
        var entry = _registry.GetAll().Single(e => e.Name == "hunger");
        Assert.Equal(0, entry.Min);
        Assert.Equal(100, entry.Max);
        Assert.Null(entry.EnumValues);
    }

    [Fact]
    public void RegisterPackProperty_CarriesEnumConstraint()
    {
        _registry.RegisterPackProperty("my-pack", "tier", "Tier", PropertyValueType.String,
            enumValues: new[] { "novice", "master" });
        var entry = _registry.GetAll().Single(e => e.Name == "tier");
        Assert.NotNull(entry.EnumValues);
        Assert.Contains("novice", entry.EnumValues!);
        Assert.Null(entry.Min);
        Assert.Null(entry.Max);
    }

    [Fact]
    public void ResolveForAdmin_UniquePackName_ReturnsFound()
    {
        _registry.RegisterPackProperty("pack-a", "loyalty", "Loyalty", PropertyValueType.Int);
        var resolution = _registry.ResolveForAdmin("loyalty");
        Assert.Equal(PropertyResolutionStatus.Found, resolution.Status);
        Assert.NotNull(resolution.Entry);
        Assert.Equal("pack-a", resolution.Entry!.Scope);
    }

    [Fact]
    public void ResolveForAdmin_SameBareNameTwoPacks_ReturnsAmbiguousWithOwners()
    {
        _registry.RegisterPackProperty("pack-a", "loyalty", "A", PropertyValueType.Int);
        _registry.RegisterPackProperty("pack-b", "loyalty", "B", PropertyValueType.Int);
        var resolution = _registry.ResolveForAdmin("loyalty");
        Assert.Equal(PropertyResolutionStatus.Ambiguous, resolution.Status);
        Assert.Null(resolution.Entry);
        Assert.Contains("pack-a", resolution.Owners);
        Assert.Contains("pack-b", resolution.Owners);
    }

    [Fact]
    public void ResolveForAdmin_QualifiedKey_ReturnsFound_EvenWhenBareNameAmbiguous()
    {
        _registry.RegisterPackProperty("pack-a", "loyalty", "A", PropertyValueType.Int);
        _registry.RegisterPackProperty("pack-b", "loyalty", "B", PropertyValueType.Int);
        var resolution = _registry.ResolveForAdmin("pack-b:loyalty");
        Assert.Equal(PropertyResolutionStatus.Found, resolution.Status);
        Assert.Equal("pack-b", resolution.Entry!.Scope);
    }

    [Fact]
    public void ResolveForAdmin_UnknownName_ReturnsNotFound()
    {
        var resolution = _registry.ResolveForAdmin("nope_prop");
        Assert.Equal(PropertyResolutionStatus.NotFound, resolution.Status);
        Assert.Null(resolution.Entry);
        Assert.Empty(resolution.Owners);
    }
}
