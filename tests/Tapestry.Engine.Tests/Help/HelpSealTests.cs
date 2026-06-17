using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Help;
using Tapestry.Engine.Registration;
using Tapestry.Shared;
using Tapestry.Shared.Help;
using Xunit;

namespace Tapestry.Engine.Tests.Help;

public class HelpSealTests
{
    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public readonly List<string> Warnings = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? ex,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning)
            {
                Warnings.Add(formatter(state, ex));
            }
        }
    }

    private sealed class FakeEdges : IPackEdgeOracle
    {
        private readonly HashSet<(string, string)> _edges = new();
        public FakeEdges Edge(string from, string to) { _edges.Add((from, to)); return this; }
        public bool DeclaresEdge(string from, string to) => _edges.Contains((from, to));
    }

    // A help service whose authored winners we seed directly, then add a real authored topic so
    // Query reflects it. We drive the policy-side capture by calling AddTopic + recording manually
    // is not exposed, so seal tests construct the inputs explicitly via a small fixture.
    private static HelpService HelpWith(params (string id, string owner, bool over)[] authored)
    {
        var svc = new HelpService(); // null policy -> eager AddTopic
        foreach (var (id, owner, _) in authored)
        {
            svc.AddTopic(new HelpTopic { Id = id, PackName = owner, Title = id, Category = "general", Body = "b" });
        }
        return svc;
    }

    private static CommandRegistry CommandsWith(params (string keyword, string owner)[] cmds)
    {
        var reg = new CommandRegistry();
        foreach (var (keyword, owner) in cmds)
        {
            reg.Register(keyword, _ => { }, packName: owner,
                argDefinitions: new Dictionary<string, ArgDefinition>
                {
                    ["target"] = new ArgDefinition { Type = "npc", Required = true }
                });
        }
        return reg;
    }

    // Variant for registering commands with no arg definitions (no-arg commands).
    private static CommandRegistry CommandsWithNoArgs(params (string keyword, string owner)[] cmds)
    {
        var reg = new CommandRegistry();
        foreach (var (keyword, owner) in cmds)
        {
            reg.Register(keyword, _ => { }, packName: owner);
        }
        return reg;
    }

    private static List<AuthoredHelpRecord> Winners(params (string id, string owner, bool over)[] xs) =>
        xs.Select(x => new AuthoredHelpRecord(x.id, x.owner, x.over, x.id + ".yaml")).ToList();

    [Fact]
    public void SameOwner_HelpEnrichesOwnCommand_Ok()
    {
        var commands = CommandsWith(("kill", "tapestry-core"));
        var help = HelpWith(("kill", "tapestry-core", false));
        var seal = new HelpSeal(help, commands, new FakeEdges(), Winners(("kill", "tapestry-core", false)));

        seal.Invoking(s => s.Seal()).Should().NotThrow();
    }

    [Fact]
    public void CrossOwner_NoOverride_BootError()
    {
        var commands = CommandsWith(("kill", "tapestry-core"));
        var help = HelpWith(("kill", "evil-pack", false));
        var seal = new HelpSeal(help, commands, new FakeEdges(), Winners(("kill", "evil-pack", false)));

        var ex = Assert.Throws<InvalidOperationException>(() => seal.Seal());
        ex.Message.Should().Contain("kill").And.Contain("tapestry-core");
    }

    [Fact]
    public void CrossOwner_Override_WithEdge_Ok()
    {
        var commands = CommandsWith(("kill", "tapestry-core"));
        var help = HelpWith(("kill", "vi-pack", true));
        var edges = new FakeEdges().Edge("vi-pack", "tapestry-core");
        var seal = new HelpSeal(help, commands, edges, Winners(("kill", "vi-pack", true)));

        seal.Invoking(s => s.Seal()).Should().NotThrow();
    }

    [Fact]
    public void CrossOwner_Override_WithoutEdge_BootError()
    {
        var commands = CommandsWith(("kill", "tapestry-core"));
        var help = HelpWith(("kill", "vi-pack", true));
        var seal = new HelpSeal(help, commands, new FakeEdges(), Winners(("kill", "vi-pack", true)));

        Assert.Throws<InvalidOperationException>(() => seal.Seal());
    }

    [Fact]
    public void Seal_StampsDerivedSyntax_OntoAuthoredCommandTopic()
    {
        // 'look' has no args -> syntax is just the keyword.
        var commands = CommandsWithNoArgs(("look", "tapestry-core"));
        var help = HelpWith(("look", "tapestry-core", false));
        var seal = new HelpSeal(help, commands, new FakeEdges(), Winners(("look", "tapestry-core", false)));

        seal.Seal();

        help.GetTopicById("look")!.Syntax.Should().ContainSingle().Which.Should().Be("look");
    }

    [Fact]
    public void ShadowCheck_PrefixOnly_DoesNotFalseTrigger()
    {
        // help topic 'ki' must NOT be treated as shadowing command 'kill' (exact-id match only).
        // Include a 'kill' winner so the coverage gate is satisfied; this test is about the shadow check.
        var commands = CommandsWith(("kill", "tapestry-core"));
        var help = HelpWith(("ki", "other-pack", false), ("kill", "tapestry-core", false));
        var seal = new HelpSeal(help, commands, new FakeEdges(),
            Winners(("ki", "other-pack", false), ("kill", "tapestry-core", false)));

        seal.Invoking(s => s.Seal()).Should().NotThrow();
    }

    [Theory]
    [InlineData("")]        // C#-engine module commands (emote, socials, say) carry an empty owner
    [InlineData("kernel")]
    [InlineData("engine")]
    public void NonPackOwnedCommand_DocumentedByPack_Ok(string commandOwner)
    {
        // Regression: a pack (here @tapestry/core) documenting an engine/kernel-registered command
        // must NOT trip the shadow gate — no pack owns an engine command exclusively. This is the
        // case that took prod down (core help/emote.yaml vs the empty-owner 'emote' command).
        var commands = CommandsWith(("emote", commandOwner));
        var help = HelpWith(("emote", "tapestry-core", false));
        var seal = new HelpSeal(help, commands, new FakeEdges(), Winners(("emote", "tapestry-core", false)));

        seal.Invoking(s => s.Seal()).Should().NotThrow();
    }

    [Fact]
    public void Coverage_MissingTopic_Strict_Throws()
    {
        var commands = CommandsWith(("flee", "tapestry-core"));
        var help = HelpWith(); // no topics authored
        var seal = new HelpSeal(help, commands, new FakeEdges(), Winners(),
            new HelpSealOptions { Strict = true }, null);

        var ex = Assert.Throws<InvalidOperationException>(() => seal.Seal());
        ex.Message.Should().Contain("flee");
    }

    [Fact]
    public void Coverage_MissingTopic_Lenient_WarnsAndContinues()
    {
        var commands = CommandsWith(("flee", "tapestry-core"), ("kill", "tapestry-core"));
        var help = HelpWith(); // both missing
        var log = new CapturingLogger<HelpSeal>();
        var seal = new HelpSeal(help, commands, new FakeEdges(), Winners(),
            new HelpSealOptions { Strict = false }, log);

        seal.Invoking(s => s.Seal()).Should().NotThrow();
        log.Warnings.Should().HaveCountGreaterThanOrEqualTo(2); // both violations surfaced, not just the first
    }

    [Fact]
    public void Category_Undeclared_Strict_Throws_WithNearestMatch()
    {
        var commands = CommandsWith(("kill", "tapestry-core"));
        // author a topic whose category 'battle' is NOT declared; 'combat' is.
        var help = HelpWith();
        help.RegisterCategory("combat", "Combat", false, "tapestry-core", 0);
        help.AddTopic(new HelpTopic
        {
            Id = "kill", Title = "kill", Category = "battle",
            PackName = "tapestry-core"
        });
        var seal = new HelpSeal(help, commands, new FakeEdges(),
            Winners(("kill", "tapestry-core", false)),
            new HelpSealOptions { Strict = true }, null);

        var ex = Assert.Throws<InvalidOperationException>(() => seal.Seal());
        ex.Message.Should().Contain("battle").And.Contain("did you mean 'combat'");
    }
}
