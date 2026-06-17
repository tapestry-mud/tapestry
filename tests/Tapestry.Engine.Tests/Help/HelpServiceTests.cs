using FluentAssertions;
using Tapestry.Engine.Help;
using Tapestry.Shared.Help;
using Xunit;

namespace Tapestry.Engine.Tests.Help;

public class HelpServiceTests
{
    private static HelpTopic MakeTopic(
        string id,
        string packName = "test-pack",
        string? role = null,
        string category = "general",
        string[]? keywords = null) =>
        new()
        {
            Id = id,
            PackName = packName,
            Title = id.Replace('-', ' '),
            Category = category,
            Brief = $"Brief for {id}",
            Body = $"Body for {id}",
            Role = role,
            Keywords = keywords?.ToList() ?? new()
        };

    [Fact]
    public void Query_ExactIdMatch_ReturnsOk()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat-basics"));

        var result = svc.Query(null, "combat-basics");

        Assert.Equal("ok", result.Status);
        Assert.Equal("combat-basics", result.Topic!.Id);
    }

    [Fact]
    public void Query_NamespacedId_ReturnsOk()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat-basics", "tapestry-example-pack"));

        var result = svc.Query(null, "tapestry-example-pack:combat-basics");

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public void Query_TitleCaseInsensitive_ReturnsOk()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("races"));

        var result = svc.Query(null, "RACES");

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public void Query_LoadOrder_NoLongerDecides_LastWriteWins()
    {
        var svc = new HelpService();
        var first = MakeTopic("combat-basics", "pack-a");
        first.Title = "Original";
        var second = MakeTopic("combat-basics", "pack-b");
        second.Title = "Override";

        // second has a LOWER loadOrder; under the old rule "first" (higher) would win.
        // Post-de-fang, load_order is ignored and the last write wins.
        svc.AddTopic(first, loadOrder: 20);
        svc.AddTopic(second, loadOrder: 10);

        var result = svc.Query(null, "combat-basics");
        Assert.Equal("Override", result.Topic!.Title);
    }

    [Fact]
    public void Query_NoMatch_ReturnsNoMatchStatus()
    {
        var svc = new HelpService();
        var result = svc.Query(null, "xyzzy");
        Assert.Equal("no_match", result.Status);
        Assert.Equal("xyzzy", result.Term);
    }

    [Fact]
    public void Query_MultipleKeywordMatches_ReturnsMultiple()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat-basics", keywords: new[] { "fighting" }));
        svc.AddTopic(MakeTopic("combat-advanced", keywords: new[] { "fighting" }));

        var result = svc.Query(null, "fighting");

        Assert.Equal("multiple", result.Status);
        Assert.Equal(2, result.Matches!.Count);
    }

    [Fact]
    public void Query_SingleKeywordMatch_ReturnsOk()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat-basics", keywords: new[] { "fighting" }));

        var result = svc.Query(null, "fighting");

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public void RoleFilter_NoPlayer_HidesPlayerTopics()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat-basics", role: "player"));

        var result = svc.Query(null, "combat-basics");

        Assert.Equal("no_match", result.Status);
    }

    [Fact]
    public void RoleFilter_NoPlayer_ShowsRolelessTopics()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("races")); // no role

        var result = svc.Query(null, "races");

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public void RoleFilter_WithPlayer_ShowsPlayerTopics()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat-basics", role: "player"));

        var result = svc.Query(Guid.NewGuid().ToString(), "combat-basics");

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public void RoleFilter_PlayerTier_HidesBuilderTopics()
    {
        var svc = new HelpService(rolesResolver: _ => new[] { "player" });
        svc.AddTopic(MakeTopic("link", role: "builder"));

        var result = svc.Query(Guid.NewGuid().ToString(), "link");

        Assert.Equal("no_match", result.Status);
    }

    [Fact]
    public void RoleFilter_BuilderRole_ShowsBuilderTopics()
    {
        var svc = new HelpService(rolesResolver: _ => new[] { "builder" });
        svc.AddTopic(MakeTopic("link", role: "builder"));

        var result = svc.Query(Guid.NewGuid().ToString(), "link");

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public void RoleFilter_AdminRole_ShowsBuilderAndAdminTopics()
    {
        // An admin entity carries roles { "admin" } only (no explicit "player"),
        // yet must see player, builder, and admin help.
        var svc = new HelpService(rolesResolver: _ => new[] { "admin" });
        svc.AddTopic(MakeTopic("kill", role: "player"));
        svc.AddTopic(MakeTopic("link", role: "builder"));
        svc.AddTopic(MakeTopic("purge", role: "admin"));

        Assert.Equal("ok", svc.Query(Guid.NewGuid().ToString(), "kill").Status);
        Assert.Equal("ok", svc.Query(Guid.NewGuid().ToString(), "link").Status);
        Assert.Equal("ok", svc.Query(Guid.NewGuid().ToString(), "purge").Status);
    }

    [Fact]
    public void Categories_ReturnsDistinctSortedList()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat", category: "combat"));
        svc.AddTopic(MakeTopic("combat-basics", category: "combat"));
        svc.AddTopic(MakeTopic("races", category: "chargen"));

        var cats = svc.Categories(null);

        Assert.Contains("chargen", cats);
        Assert.Contains("combat", cats);
        Assert.Equal(2, cats.Count);
    }

    [Fact]
    public void List_ReturnsSummariesForCategory()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat", category: "combat"));
        svc.AddTopic(MakeTopic("combat-basics", category: "combat"));

        var list = svc.List(null, "combat");

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void List_RoleFilter_HidesPlayerTopics_WhenNoPlayer()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("combat-basics", category: "combat", role: "player"));

        var list = svc.List(null, "combat");

        Assert.Empty(list);
    }

    [Fact]
    public void List_RolelessTopic_VisibleToPlayer()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("races", category: "creation"));

        var list = svc.List(Guid.NewGuid().ToString(), "creation");

        Assert.Single(list);
    }

    [Fact]
    public void Categories_And_List_Consistent_For_Roleless_Topic()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("races", category: "creation"));

        var entityId = Guid.NewGuid().ToString();
        var cats = svc.Categories(entityId);
        var list = svc.List(entityId, "creation");

        Assert.Contains("creation", cats);
        Assert.Single(list);
    }

    [Fact]
    public void HelpTopic_OverrideField_DeserializesFromYaml()
    {
        var yaml = "id: combat\ntitle: Combat\noverride: true\n";
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var topic = deserializer.Deserialize<Tapestry.Shared.Help.HelpTopic>(yaml);

        Assert.True(topic.Override);
    }

    [Fact]
    public void GetTopicById_ExactMatch_IgnoresRoleGate()
    {
        var svc = new HelpService();
        svc.AddTopic(MakeTopic("smite", role: "admin"));

        var topic = svc.GetTopicById("smite");

        Assert.NotNull(topic);
        Assert.Equal("smite", topic!.Id);
    }

    [Fact]
    public void GetTopicById_NoMatch_ReturnsNull()
    {
        var svc = new HelpService();
        Assert.Null(svc.GetTopicById("nope"));
    }

    [Fact]
    public void List_ExcludesHiddenCategoryMembers_ButQueryStillResolves()
    {
        var svc = new HelpService();
        svc.RegisterCategory("social", "Socials", true, "core", 0);
        svc.AddTopic(MakeTopic("wave", category: "social"));

        svc.List(null, "social").Should().BeEmpty();        // browse hides it
        Assert.NotNull(svc.GetTopicById("wave"));           // direct lookup resolves
        Assert.Equal("ok", svc.Query(null, "wave").Status); // direct help works
    }

    [Fact]
    public void List_ExcludesIndividuallyHiddenTopic_ButQueryStillResolves()
    {
        var svc = new HelpService();
        svc.RegisterCategory("character", "Character", false, "core", 0);
        svc.AddTopic(MakeTopic("utf8", category: "character"));
        svc.GetTopicById("utf8")!.Hidden = true; // visible category, topic itself hidden

        svc.List(null, "character").Should().NotContain(s => s.Id == "utf8");
        Assert.Equal("ok", svc.Query(null, "utf8").Status);
    }

    [Fact]
    public void Categories_ExcludesHiddenCategory()
    {
        var svc = new HelpService();
        svc.RegisterCategory("communication", "Communication", false, "core", 0);
        svc.RegisterCategory("social", "Socials", true, "core", 0);
        svc.AddTopic(MakeTopic("say", category: "communication"));
        svc.AddTopic(MakeTopic("wave", category: "social"));

        svc.Categories(null).Should().Contain("communication").And.NotContain("social");
    }

    [Fact]
    public void IsListed_TrueOnlyWhenNeitherHidden()
    {
        var svc = new HelpService();
        svc.RegisterCategory("communication", "Communication", false, "core", 0);
        svc.RegisterCategory("social", "Socials", true, "core", 0);
        svc.AddTopic(MakeTopic("say", category: "communication"));
        svc.AddTopic(MakeTopic("wave", category: "social"));
        svc.AddTopic(MakeTopic("utf8", category: "communication"));
        svc.GetTopicById("utf8")!.Hidden = true;

        svc.IsListed("say").Should().BeTrue();
        svc.IsListed("wave").Should().BeFalse();  // category hidden
        svc.IsListed("utf8").Should().BeFalse();  // topic hidden
        svc.IsListed("nope").Should().BeFalse();  // no topic
    }

    [Fact]
    public void VisibleDeclaredCategories_DeclarationOrder_ExcludesHidden_KeepsLabels()
    {
        var svc = new HelpService();
        svc.RegisterCategory("movement", "Movement", false, "tapestry-core", 0);
        svc.RegisterCategory("shop", "Trade", false, "tapestry-core", 0);
        svc.RegisterCategory("social", "Socials", true, "tapestry-core", 0);
        svc.RegisterCategory("admin", "Immortal", false, "tapestry-core", 0);

        var cats = svc.VisibleDeclaredCategories;

        cats.Select(c => c.Id).Should().ContainInOrder("movement", "shop", "admin");
        cats.Should().NotContain(c => c.Id == "social");          // hidden excluded
        cats.Single(c => c.Id == "shop").Label.Should().Be("Trade"); // label, not capitalized id
        cats.Single(c => c.Id == "admin").Label.Should().Be("Immortal");
    }
}
