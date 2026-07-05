using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

/// <summary>
/// Pins the DamageVerbs MinDamage boundaries (2026-07-04 low-level combat-feel
/// retune). Verbs key on ABSOLUTE damage - the progression channel. A geared
/// level-1 hit (~6-7) must read "grazes"/"hits"; the decorated tiers stay
/// gear/spell territory. If a retune moves a boundary, this table moves with it
/// - the two are one change.
/// </summary>
public class DamageVerbLadderTests
{
    private static JintRuntime BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return rt;
    }

    private static string VerbOf(JintRuntime rt, int damage)
    {
        var formatted = (string)EsmTest.Eval(rt, $"tapestry.combat.formatDamageVerb({damage})")!;
        var m = Regex.Match(formatted, "<[a-z_]+_verb>(.*?)</[a-z_]+_verb>");
        Assert.True(m.Success, $"no verb tag in '{formatted}' for damage {damage}");
        return m.Groups[1].Value;
    }

    [Fact]
    public void DamageVerbLadder_BoundariesArePinned()
    {
        // (minDamage, verb) - the full 20-rung ladder, ascending.
        var ladder = new (int Min, string Verb)[]
        {
            (0, "tickles"),
            (2, "barely scratches"),
            (4, "scratches"),
            (6, "grazes"),
            (9, "hits"),
            (13, "injures"),
            (17, "wounds"),
            (22, "mauls"),
            (29, "decimates"),
            (37, "devastates"),
            (47, "MAIMS"),
            (59, "MUTILATES"),
            (73, "DISMEMBERS"),
            (91, "MASSACRES"),
            (116, "ANNIHILATES"),
            (146, "OBLITERATES"),
            (191, "DESTROYS"),
            (241, "PULVERIZES"),
            (301, "ERADICATES"),
            (421, "VAPORIZES"),
        };

        var rt = BuildRuntime();

        for (var i = 0; i < ladder.Length; i++)
        {
            // At the boundary: the rung's own verb.
            Assert.Equal(ladder[i].Verb, VerbOf(rt, ladder[i].Min));
            // One below the boundary: the previous rung's verb.
            if (i > 0)
            {
                Assert.Equal(ladder[i - 1].Verb, VerbOf(rt, ladder[i].Min - 1));
            }
        }

        // Above the top rung stays the top rung; at/below zero is the floor.
        Assert.Equal("VAPORIZES", VerbOf(rt, 10_000));
        Assert.Equal("tickles", VerbOf(rt, 1));
        Assert.Equal("tickles", VerbOf(rt, -5));
    }

    [Fact]
    public void DamageVerbLadder_GearedLevelOneHitsReadAsProgress()
    {
        // The complaint that drove the retune: a level-1 character with the
        // starter kit (avg ~6.5 damage per hit) read "scratches/grazes" all
        // fight. Average rolls must now read "grazes"/"hits" and a good roll
        // (9-12 on 1d12 or 2d6 high) reads "hits"; 13+ reads "injures".
        var rt = BuildRuntime();
        Assert.Equal("grazes", VerbOf(rt, 6));
        Assert.Equal("grazes", VerbOf(rt, 7));
        Assert.Equal("hits", VerbOf(rt, 9));
        Assert.Equal("hits", VerbOf(rt, 12));
        Assert.Equal("injures", VerbOf(rt, 13));
    }
}
