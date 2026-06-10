using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class CommandRegistryTests
{
    [Fact]
    public void Register_AndResolve()
    {
        var registry = new CommandRegistry();
        Action<ActorContext> handler = (_) => { };
        registry.Register("look", handler, aliases: ["l"], priority: 0, packName: "core");
        registry.Resolve("look").Should().NotBeNull();
        registry.Resolve("l").Should().NotBeNull();
        registry.Resolve("look")!.ActorHandler.Should().Be(handler);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNotRegistered()
    {
        var registry = new CommandRegistry();
        registry.Resolve("nonexistent").Should().BeNull();
    }

    [Fact]
    public void HigherPriority_WinsConflict()
    {
        var registry = new CommandRegistry();
        Action<ActorContext> lowHandler = (_) => { };
        Action<ActorContext> highHandler = (_) => { };
        registry.Register("look", lowHandler, priority: 10, packName: "base");
        registry.Register("look", highHandler, priority: 100, packName: "override");
        registry.Resolve("look")!.ActorHandler.Should().Be(highHandler);
    }

    [Fact]
    public void CaseInsensitive()
    {
        var registry = new CommandRegistry();
        Action<ActorContext> handler = (_) => { };
        registry.Register("Look", handler, packName: "core");
        registry.Resolve("look").Should().NotBeNull();
        registry.Resolve("LOOK").Should().NotBeNull();
    }

    [Fact]
    public void PrefixMatch_SingleCharacter()
    {
        var registry = new CommandRegistry();
        Action<ActorContext> handler = (_) => { };
        registry.Register("north", handler, packName: "core");

        registry.Resolve("n").Should().NotBeNull();
        registry.Resolve("n")!.Keyword.Should().Be("north");
        registry.Resolve("no").Should().NotBeNull();
        registry.Resolve("nor").Should().NotBeNull();
        registry.Resolve("nort").Should().NotBeNull();
    }

    [Fact]
    public void PrefixMatch_ExactMatchWinsOverPrefix()
    {
        var registry = new CommandRegistry();
        Action<ActorContext> nHandler = (_) => { };
        Action<ActorContext> northHandler = (_) => { };
        // Register "n" as an explicit command AND "north"
        registry.Register("n", nHandler, packName: "core");
        registry.Register("north", northHandler, packName: "core");

        // Exact match "n" should win over prefix match to "north"
        registry.Resolve("n")!.ActorHandler.Should().Be(nHandler);
        // "no" should prefix match to "north"
        registry.Resolve("no")!.ActorHandler.Should().Be(northHandler);
    }

    [Fact]
    public void PrefixMatch_AmbiguousPrefix_HighestPriorityWins()
    {
        var registry = new CommandRegistry();
        Action<ActorContext> southHandler = (_) => { };
        Action<ActorContext> sayHandler = (_) => { };
        Action<ActorContext> scoreHandler = (_) => { };
        // south registered at priority 0 (movement), say at 0, score at 0
        // When all same priority, first registered wins
        registry.Register("south", southHandler, priority: 0, packName: "core");
        registry.Register("say", sayHandler, priority: 0, packName: "core");
        registry.Register("score", scoreHandler, priority: 0, packName: "core");

        // "s" is ambiguous — south was registered first at same priority
        registry.Resolve("s")!.Keyword.Should().Be("south");
        // "sa" is unambiguous — only "say" starts with "sa"
        registry.Resolve("sa")!.ActorHandler.Should().Be(sayHandler);
        // "sc" is unambiguous — only "score"
        registry.Resolve("sc")!.ActorHandler.Should().Be(scoreHandler);
        // "so" is unambiguous — only "south"
        registry.Resolve("so")!.ActorHandler.Should().Be(southHandler);
    }

    [Fact]
    public void PrefixMatch_NoMatch_ReturnsNull()
    {
        var registry = new CommandRegistry();
        registry.Register("north", (_) => { }, packName: "core");

        registry.Resolve("x").Should().BeNull();
        registry.Resolve("nz").Should().BeNull();
    }

    [Fact]
    public void PrefixMatch_MatchesKeywordsNotAliases()
    {
        var registry = new CommandRegistry();
        Action<ActorContext> lookHandler = (_) => { };
        // "l" is an explicit alias for look
        registry.Register("look", lookHandler, aliases: ["l"], packName: "core");

        // "lo" should prefix-match "look"
        registry.Resolve("lo")!.ActorHandler.Should().Be(lookHandler);
        // "l" should exact-match the alias
        registry.Resolve("l")!.ActorHandler.Should().Be(lookHandler);
    }

    [Fact]
    public void Register_WithNoRoles_DefaultsToPlayer()
    {
        {
            var registry = new CommandRegistry();
            registry.Register("test", _ => { });
            var reg = registry.Resolve("test");
            Assert.NotNull(reg);
            Assert.Contains("player", reg!.Roles);
        }
    }

    [Fact]
    public void Register_WithExplicitRoles_StoresRoles()
    {
        {
            var registry = new CommandRegistry();
            registry.Register("test", _ => { }, roles: ["mob"]);
            var reg = registry.Resolve("test");
            Assert.NotNull(reg);
            Assert.Contains("mob", reg!.Roles);
        }
    }

    [Fact]
    public void Resolve_WithSource_Mob_ExcludesPlayerOnlyCommands()
    {
        {
            var registry = new CommandRegistry();
            registry.Register("quit", _ => { }, roles: ["player"]);
            var result = registry.Resolve("quit", "mob");
            Assert.Null(result);
        }
    }

    [Fact]
    public void Resolve_WithSource_Mob_IncludesMobRoleCommands()
    {
        {
            var registry = new CommandRegistry();
            registry.Register("say", _ => { }, roles: ["player", "mob"]);
            var result = registry.Resolve("say", "mob");
            Assert.NotNull(result);
        }
    }

    [Fact]
    public void Resolve_WithSource_Player_ExcludesMobOnlyCommands()
    {
        {
            var registry = new CommandRegistry();
            registry.Register("mobsay", _ => { }, roles: ["mob"]);
            var result = registry.Resolve("mobsay", "player");
            Assert.Null(result);
        }
    }

    [Fact]
    public void Resolve_WithSource_Null_ReturnsAllCommands()
    {
        {
            var registry = new CommandRegistry();
            registry.Register("anything", _ => { }, roles: ["mob"]);
            var result = registry.Resolve("anything", null);
            Assert.NotNull(result);
        }
    }

    // ── #98: role-aware resolution ─────────────────────────────────────────
    // Election must filter by actor type FIRST, then pick the winner. The old order
    // (elect globally, role-filter after) let an eagerly-registered mob-only verb win
    // the keyword and then fail the player's role check -> "Huh?" for players.

    [Fact]
    public void Resolve_PlayerSource_SkipsMobOnlyRegistration_EvenWhenRegisteredFirst()
    {
        var registry = new CommandRegistry();
        registry.Register("say", _ => { }, roles: ["mob"], packName: "tapestry-core");
        registry.Register("say", _ => { }, roles: ["player", "mob"], packName: "tapestry-core");
        var reg = registry.Resolve("say", "player");
        reg.Should().NotBeNull();
        reg!.Roles.Should().Contain("player");
    }

    [Fact]
    public void Resolve_MobSource_PrefersMobSpecificRegistration_RegardlessOfOrder()
    {
        var registry = new CommandRegistry();
        registry.Register("say", _ => { }, roles: ["player", "mob"]);
        registry.Register("say", _ => { }, roles: ["mob"]);
        var reg = registry.Resolve("say", "mob");
        reg.Should().NotBeNull();
        reg!.Roles.Should().BeEquivalentTo(new[] { "mob" }); // specificity beats breadth
    }

    [Fact]
    public void Resolve_MobSource_PrefersMobSpecific_WhenMobRegisteredFirst()
    {
        var registry = new CommandRegistry();
        registry.Register("say", _ => { }, roles: ["mob"]);
        registry.Register("say", _ => { }, roles: ["player", "mob"]);
        registry.Resolve("say", "mob")!.Roles.Should().BeEquivalentTo(new[] { "mob" }); // order-independent
    }

    [Fact]
    public void Resolve_RoleBlockedExactMatch_FallsThroughToPrefix()
    {
        var registry = new CommandRegistry();
        registry.Register("sa", _ => { }, roles: ["mob"]);
        registry.Register("salute", _ => { }, roles: ["player"]);
        var reg = registry.Resolve("sa", "player");
        reg.Should().NotBeNull();
        reg!.Keyword.Should().Be("salute");
    }

    [Fact]
    public void Resolve_PlayerSource_AliasOnPlayerRegistration_StillResolves()
    {
        var registry = new CommandRegistry();
        registry.Register("say", _ => { }, roles: ["mob"]);
        registry.Register("say", _ => { }, aliases: ["'"], roles: ["player", "mob"]);
        registry.Resolve("'", "player").Should().NotBeNull();
    }

    [Fact]
    public void Resolve_RoleBlind_SingleArg_ContractUnchanged()
    {
        var registry = new CommandRegistry();
        registry.Register("say", _ => { }, roles: ["mob"]);
        registry.Resolve("say").Should().NotBeNull(); // role-blind callers (HelpSeal, validators) see everything
    }

    [Fact]
    public void Resolve_PrefixElection_PlayerVerbBeatsAdminOnlyCommand()
    {
        // Privilege narrowness is not actor-type specificity: an admin-only registration
        // (visible to players, privilege-gated later) must not outrank a broader
        // player verb on cross-keyword prefix election.
        var registry = new CommandRegistry();
        registry.Register("abilities", _ => { }, roles: ["player", "mob"]);
        registry.Register("abjure", _ => { }, roles: ["admin"]);
        var reg = registry.Resolve("ab", "player");
        reg.Should().NotBeNull();
        reg!.Keyword.Should().Be("abilities");
    }

    [Fact]
    public void Resolve_MobSource_HighPriorityDualRoleBeatsLowPriorityMobOnly()
    {
        var registry = new CommandRegistry();
        registry.Register("say", _ => { }, roles: ["player", "mob"], priority: 10);
        registry.Register("say", _ => { }, roles: ["mob"], priority: 0);
        var reg = registry.Resolve("say", "mob");
        reg.Should().NotBeNull();
        reg!.Priority.Should().Be(10); // priority outranks specificity -- the override contract
    }
}
