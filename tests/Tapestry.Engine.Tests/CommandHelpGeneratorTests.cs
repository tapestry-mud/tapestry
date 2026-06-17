using Tapestry.Engine;
using Xunit;

namespace Tapestry.Engine.Tests;

public class CommandHelpGeneratorTests
{
    [Fact]
    public void BuildSyntax_NoArgDefs_ReturnsJustKeyword()
    {
        var reg = MakeRegistration("north", argDefs: null);
        Assert.Equal("north", CommandHelpGenerator.BuildSyntax(reg));
    }

    [Fact]
    public void BuildSyntax_SimpleArgs_BuildsSyntaxLine()
    {
        var reg = MakeRegistration("give", argDefs: new()
        {
            ["item"] = new ArgDefinition { Type = "inventory", Required = true },
            ["target"] = new ArgDefinition { Type = "entity", Required = true, Prepositions = ["to"] }
        });

        Assert.Equal("give [item] to [target]", CommandHelpGenerator.BuildSyntax(reg));
    }

    [Fact]
    public void BuildSyntax_OptionalArg_WrapsInParens()
    {
        var reg = MakeRegistration("look", argDefs: new()
        {
            ["target"] = new ArgDefinition { Type = "entity", Required = false }
        });

        Assert.Equal("look ([target])", CommandHelpGenerator.BuildSyntax(reg));
    }

    [Fact]
    public void BuildSyntax_BulkArg_ShowsBulkPattern()
    {
        var reg = MakeRegistration("drop", argDefs: new()
        {
            ["item"] = new ArgDefinition { Type = "inventory", Required = true, Bulk = true }
        });

        Assert.Equal("drop [item | all | all.item]", CommandHelpGenerator.BuildSyntax(reg));
    }

    private static CommandRegistration MakeRegistration(
        string keyword, Dictionary<string, ArgDefinition>? argDefs)
    {
        return new CommandRegistration
        {
            Keyword = keyword,
            ActorHandler = _ => { },
            Roles = ["player"],
            ArgDefinitions = argDefs
        };
    }
}
