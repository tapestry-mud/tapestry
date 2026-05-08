using Tapestry.Engine;
using Xunit;

namespace Tapestry.Engine.Tests;

public class CommandHelpGeneratorTests
{
    [Fact]
    public void GenerateFor_NoArgDefs_ReturnsNull()
    {
        var reg = MakeRegistration("north", "Go north.", argDefs: null);
        Assert.Null(CommandHelpGenerator.GenerateFor(reg));
    }

    [Fact]
    public void GenerateFor_SimpleArgs_BuildsSyntaxLine()
    {
        var reg = MakeRegistration("give", "Give an item.", argDefs: new()
        {
            ["item"] = new ArgDefinition { Type = "inventory", Required = true },
            ["target"] = new ArgDefinition { Type = "entity", Required = true, Prepositions = ["to"] }
        });

        var topic = CommandHelpGenerator.GenerateFor(reg);

        Assert.NotNull(topic);
        Assert.Contains("give [item] to [target]", topic!.Syntax);
    }

    [Fact]
    public void GenerateFor_OptionalArg_WrapsInParens()
    {
        var reg = MakeRegistration("look", "Look around.", argDefs: new()
        {
            ["target"] = new ArgDefinition { Type = "entity", Required = false }
        });

        var topic = CommandHelpGenerator.GenerateFor(reg);

        Assert.NotNull(topic);
        Assert.Contains("look ([target])", topic!.Syntax);
    }

    [Fact]
    public void GenerateFor_BulkArg_ShowsBulkPattern()
    {
        var reg = MakeRegistration("drop", "Drop items.", argDefs: new()
        {
            ["item"] = new ArgDefinition { Type = "inventory", Required = true, Bulk = true }
        });

        var topic = CommandHelpGenerator.GenerateFor(reg);

        Assert.NotNull(topic);
        Assert.Contains("drop [item | all | all.item]", topic!.Syntax);
    }

    [Fact]
    public void GenerateFor_SetsIdFromCommandKeyword()
    {
        var reg = MakeRegistration("give", "Give an item.", argDefs: new()
        {
            ["item"] = new ArgDefinition { Type = "inventory", Required = true }
        });

        var topic = CommandHelpGenerator.GenerateFor(reg);

        Assert.Equal("give", topic!.Id);
    }

    private static CommandRegistration MakeRegistration(
        string keyword, string description, Dictionary<string, ArgDefinition>? argDefs)
    {
        return new CommandRegistration
        {
            Keyword = keyword,
            Handler = _ => { },
            Description = description,
            Category = "general",
            Roles = ["player"],
            ArgDefinitions = argDefs
        };
    }
}
