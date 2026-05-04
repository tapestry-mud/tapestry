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
        svc.AddTopic(MakeTopic("combat-basics", "example-pack"));

        var result = svc.Query(null, "example-pack:combat-basics");

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
    public void Query_HigherLoadOrderWins_OnCollision()
    {
        var svc = new HelpService();
        var low = MakeTopic("combat-basics", "pack-a");
        low.Title = "Original";
        var high = MakeTopic("combat-basics", "pack-b");
        high.Title = "Override";

        svc.AddTopic(low, loadOrder: 10);
        svc.AddTopic(high, loadOrder: 20);

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
}
