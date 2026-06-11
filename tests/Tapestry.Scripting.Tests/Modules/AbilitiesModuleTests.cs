using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class AbilitiesModuleTests
{
    // register() defers the registry write to the RegistrationPolicy seal barrier;
    // tests that register via JS call policy.Resolve() before reading back.
    private (JintRuntime rt, AbilityRegistry reg, RegistrationPolicy policy) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<AbilityRegistry>(),
                provider.GetRequiredService<RegistrationPolicy>());
    }

    [Fact]
    public void Register_CapturesSourceFileFromCurrentSource()
    {
        var (rt, reg, policy) = BuildRuntime();
        rt.Execute(
            "tapestry.abilities.register({ id: 'kick', name: 'Kick', type: 'active', category: 'skill', handler: function(){} });",
            "test-pack",
            "scripts/abilities/skills.js"
        );
        policy.Resolve();
        var def = reg.Get("kick");
        Assert.Equal("scripts/abilities/skills.js", def!.SourceFile);
    }

    [Fact]
    public void Register_CapturesShortName()
    {
        var (rt, reg, policy) = BuildRuntime();
        rt.Execute(@"
            tapestry.abilities.register({
                id: 'heron_wading_in_the_rushes',
                name: 'Heron Wading in the Rushes',
                short_name: 'Heron',
                type: 'active',
                category: 'skill',
                handler: function(){}
            });
        ");
        policy.Resolve();
        var def = reg.Get("heron_wading_in_the_rushes");
        Assert.Equal("Heron", def!.ShortName);
    }

    [Fact]
    public void Register_ShortNameDefaultsToNullWhenOmitted()
    {
        var (rt, reg, policy) = BuildRuntime();
        rt.Execute(@"
            tapestry.abilities.register({
                id: 'bash',
                name: 'Bash',
                type: 'active',
                category: 'skill',
                handler: function(){}
            });
        ");
        policy.Resolve();
        var def = reg.Get("bash");
        Assert.Null(def!.ShortName);
    }

    [Fact]
    public void Register_FailureGainMultiplier_RoundTrips()
    {
        var (rt, reg, policy) = BuildRuntime();
        rt.Execute(@"
            tapestry.abilities.register({
                id: 'dodge',
                name: 'Dodge',
                failure_gain_multiplier: 0.1
            });
        ");
        policy.Resolve();
        var def = reg.Get("dodge");
        Assert.NotNull(def);
        Assert.Equal(0.1, def!.FailureProficiencyGainMultiplier);
    }

    [Fact]
    public void Register_FailureGainMultiplier_DefaultsWhenOmitted()
    {
        var (rt, reg, policy) = BuildRuntime();
        rt.Execute(@"
            tapestry.abilities.register({ id: 'parry', name: 'Parry' });
        ");
        policy.Resolve();
        var def = reg.Get("parry");
        Assert.NotNull(def);
        Assert.Equal(0.25, def!.FailureProficiencyGainMultiplier);
    }

    [Fact]
    public void Search_ByNameFragment_ReturnsMatchingRows()
    {
        var (rt, reg, policy) = BuildRuntime();
        rt.Execute(@"
            tapestry.abilities.register({ id: 'fireball', name: 'Fireball', type: 'active', category: 'spell' });
            tapestry.abilities.register({ id: 'fire_shield', name: 'Fire Shield', type: 'passive', category: 'spell' });
            tapestry.abilities.register({ id: 'bash', name: 'Bash', type: 'active', category: 'skill' });
        ");
        policy.Resolve();

        var json = rt.Evaluate("JSON.stringify(tapestry.abilities.search('fire'))")?.ToString() ?? "";
        Assert.Contains("fireball", json);
        Assert.Contains("fire_shield", json);
        Assert.DoesNotContain("bash", json);
    }

    [Fact]
    public void Search_AllKeyword_ReturnsEverything()
    {
        var (rt, reg, policy) = BuildRuntime();
        rt.Execute(@"
            tapestry.abilities.register({ id: 'kick', name: 'Kick', type: 'active', category: 'skill' });
            tapestry.abilities.register({ id: 'mend', name: 'Mend', type: 'active', category: 'spell' });
        ");
        policy.Resolve();

        var json = rt.Evaluate("JSON.stringify(tapestry.abilities.search('all'))")?.ToString() ?? "";
        Assert.Contains("kick", json);
        Assert.Contains("mend", json);
    }

    [Fact]
    public void Search_WhitespaceOrNoMatch_ReturnsEmpty()
    {
        var (rt, reg, policy) = BuildRuntime();
        rt.Execute(@"
            tapestry.abilities.register({ id: 'dodge', name: 'Dodge', type: 'passive', category: 'skill' });
        ");
        policy.Resolve();

        var whitespaceJson = rt.Evaluate("JSON.stringify(tapestry.abilities.search('   '))")?.ToString() ?? "";
        Assert.Equal("[]", whitespaceJson);

        var noMatchJson = rt.Evaluate("JSON.stringify(tapestry.abilities.search('zzznomatch'))")?.ToString() ?? "";
        Assert.Equal("[]", noMatchJson);
    }
}
