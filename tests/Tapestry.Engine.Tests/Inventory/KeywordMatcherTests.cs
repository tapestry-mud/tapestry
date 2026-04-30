// tests/Tapestry.Engine.Tests/Inventory/KeywordMatcherTests.cs
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Inventory;

namespace Tapestry.Engine.Tests.Inventory;

public class KeywordMatcherTests
{
    [Fact]
    public void FindByKeyword_MatchesTag()
    {
        var sword = new Entity("item:weapon", "an iron sword");
        sword.AddTag("sword");
        sword.AddTag("iron");

        var entities = new List<Entity> { sword };
        KeywordMatcher.FindByKeyword(entities, "sword").Should().Be(sword);
    }

    [Fact]
    public void FindByKeyword_ReturnsNull_WhenNoMatch()
    {
        var sword = new Entity("item:weapon", "an iron sword");
        sword.AddTag("sword");
        var entities = new List<Entity> { sword };
        KeywordMatcher.FindByKeyword(entities, "axe").Should().BeNull();
    }

    [Fact]
    public void FindByKeyword_Ordinal_ReturnsNth()
    {
        var ring1 = new Entity("item:armor", "a gold ring");
        ring1.AddTag("ring");
        var ring2 = new Entity("item:armor", "a silver ring");
        ring2.AddTag("ring");

        var entities = new List<Entity> { ring1, ring2 };
        KeywordMatcher.FindByKeyword(entities, "2.ring").Should().Be(ring2);
    }

    [Fact]
    public void FindByKeyword_Ordinal_OutOfRange_ReturnsNull()
    {
        var ring = new Entity("item:armor", "a gold ring");
        ring.AddTag("ring");
        var entities = new List<Entity> { ring };
        KeywordMatcher.FindByKeyword(entities, "3.ring").Should().BeNull();
    }

    [Fact]
    public void FindAllByKeyword_ReturnsAllMatches()
    {
        var s1 = new Entity("item:weapon", "iron sword");
        s1.AddTag("sword");
        var s2 = new Entity("item:weapon", "steel sword");
        s2.AddTag("sword");
        var axe = new Entity("item:weapon", "axe");
        axe.AddTag("axe");

        var entities = new List<Entity> { s1, s2, axe };
        KeywordMatcher.FindAllByKeyword(entities, "sword").Should().HaveCount(2);
    }

    [Fact]
    public void FindByKeyword_AllDotKeyword_ReturnsAll()
    {
        var s1 = new Entity("item:weapon", "iron sword");
        s1.AddTag("sword");
        var s2 = new Entity("item:weapon", "steel sword");
        s2.AddTag("sword");

        var entities = new List<Entity> { s1, s2 };
        KeywordMatcher.FindAllByKeyword(entities, "all.sword").Should().HaveCount(2);
    }

    [Fact]
    public void FindByKeyword_PrefixMatchesTag()
    {
        var helm = new Entity("item:armor", "a leather helm");
        helm.AddTag("helm");
        helm.AddTag("leather");

        var entities = new List<Entity> { helm };
        KeywordMatcher.FindByKeyword(entities, "hel").Should().Be(helm);
    }

    [Fact]
    public void FindByKeyword_ExactMatchWinsOverPrefix()
    {
        var sword = new Entity("item:weapon", "an iron sword");
        sword.AddTag("sword");
        sword.AddTag("iron");
        var swordfish = new Entity("item:misc", "a swordfish");
        swordfish.AddTag("swordfish");

        var entities = new List<Entity> { swordfish, sword };
        KeywordMatcher.FindByKeyword(entities, "sword").Should().Be(sword);
    }

    [Fact]
    public void FindByKeyword_All_ReturnsEverything()
    {
        var s1 = new Entity("item:weapon", "sword");
        s1.AddTag("item");
        var s2 = new Entity("item:armor", "helm");
        s2.AddTag("item");

        var entities = new List<Entity> { s1, s2 };
        KeywordMatcher.FindAllByKeyword(entities, "all").Should().HaveCount(2);
    }
}
