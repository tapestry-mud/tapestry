using Tapestry.Engine;

namespace Tapestry.Engine.Tests.Commands;

public class CommandRegistryTests
{
    [Fact]
    public void Register_CapturesDescription()
    {
        var registry = new CommandRegistry();
        registry.Register("look", _ => { }, description: "Look at your surroundings.");
        var reg = registry.Resolve("look");
        Assert.Equal("Look at your surroundings.", reg!.Description);
    }

    [Fact]
    public void Register_CapturesCategory()
    {
        var registry = new CommandRegistry();
        registry.Register("look", _ => { }, category: "movement");
        var reg = registry.Resolve("look");
        Assert.Equal("movement", reg!.Category);
    }

    [Fact]
    public void Register_CapturesSourceFile()
    {
        var registry = new CommandRegistry();
        registry.Register("look", _ => { }, sourceFile: "scripts/commands/look.js");
        var reg = registry.Resolve("look");
        Assert.Equal("scripts/commands/look.js", reg!.SourceFile);
    }

    [Fact]
    public void Register_CapturesVisibleTo()
    {
        var registry = new CommandRegistry();
        Func<Entity, bool> pred = _ => false;
        registry.Register("secret", _ => { }, visibleTo: pred);
        var reg = registry.Resolve("secret");
        Assert.NotNull(reg!.VisibleTo);
    }

    [Fact]
    public void Resolve_StillWorksForHiddenCommand()
    {
        var registry = new CommandRegistry();
        bool handlerCalled = false;
        registry.Register("secret", _ => { handlerCalled = true; }, visibleTo: _ => false);
        var reg = registry.Resolve("secret");
        Assert.NotNull(reg);
        reg!.Handler(null!);
        Assert.True(handlerCalled);
    }

    [Fact]
    public void PrimaryKeywords_ReturnsOnlyPrimaryKeywordsNotAliases()
    {
        var registry = new CommandRegistry();
        registry.Register("inventory", _ => { }, aliases: ["i", "inv"]);
        registry.Register("look", _ => { }, aliases: ["l"]);
        var keywords = registry.PrimaryKeywords.ToList();
        Assert.Contains("inventory", keywords);
        Assert.Contains("look", keywords);
        Assert.DoesNotContain("i", keywords);
        Assert.DoesNotContain("l", keywords);
        Assert.DoesNotContain("inv", keywords);
    }

    [Fact]
    public void PrimaryKeywords_DeduplicatesWhenSameKeywordRegisteredTwice()
    {
        var registry = new CommandRegistry();
        registry.Register("look", _ => { }, priority: 0);
        registry.Register("look", _ => { }, priority: 10);
        var keywords = registry.PrimaryKeywords.ToList();
        Assert.Single(keywords, k => k.Equals("look", StringComparison.OrdinalIgnoreCase));
    }
}
