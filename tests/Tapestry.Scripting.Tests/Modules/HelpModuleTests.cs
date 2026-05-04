using Jint.Native;
using Tapestry.Engine.Help;
using Tapestry.Scripting.Modules;
using Tapestry.Shared.Help;

namespace Tapestry.Scripting.Tests.Modules;

public class HelpModuleTests
{
    private static HelpService BuildService(params (string id, string? role)[] topics)
    {
        var svc = new HelpService();
        foreach (var (id, role) in topics)
        {
            svc.AddTopic(new HelpTopic
            {
                Id = id,
                PackName = "test",
                Title = id,
                Category = "general",
                Brief = $"Brief for {id}",
                Body = $"Body for {id}",
                Role = role
            });
        }
        return svc;
    }

    [Fact]
    public void Namespace_IsHelp()
    {
        var module = new HelpModule(new HelpService());
        Assert.Equal("help", module.Namespace);
    }

    [Fact]
    public void QueryDirect_NoPlayer_ReturnsRolelessTopic()
    {
        var svc = BuildService(("races", null));
        var module = new HelpModule(svc);

        var result = module.QueryDirect(null, "races");

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public void QueryDirect_WithGuid_ReturnsPlayerTopic()
    {
        var svc = BuildService(("combat", "player"));
        var module = new HelpModule(svc);

        var result = module.QueryDirect(Guid.NewGuid().ToString(), "combat");

        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public void CategoriesDirect_ReturnsList()
    {
        var svc = BuildService(("races", null));
        var module = new HelpModule(svc);

        var cats = module.CategoriesDirect(null);

        Assert.Contains("general", cats);
    }

    [Fact]
    public void ResolveArgs_GuidFirstArg_ReturnsEntityIdAndTerm()
    {
        var guid = Guid.NewGuid().ToString();
        var engine = new Jint.Engine();
        var first = JsValue.FromObject(engine, guid);
        var second = JsValue.FromObject(engine, "combat");

        var (entityId, term) = HelpModule.ResolveArgs(first, second);

        Assert.Equal(guid, entityId);
        Assert.Equal("combat", term);
    }

    [Fact]
    public void ResolveArgs_NonGuidFirstArg_ReturnsTerm()
    {
        var engine = new Jint.Engine();
        var first = JsValue.FromObject(engine, "races");
        var second = JsValue.Undefined;

        var (entityId, term) = HelpModule.ResolveArgs(first, second);

        Assert.Null(entityId);
        Assert.Equal("races", term);
    }
}
