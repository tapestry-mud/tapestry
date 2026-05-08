using Tapestry.Engine;
using Xunit;

namespace Tapestry.Engine.Tests;

public class CommandInputParserTests
{
    [Fact]
    public void Parse_SingleCommand_ReturnsSingleEntry()
    {
        var result = CommandInputParser.Parse("give blade lan");
        Assert.Single(result);
        Assert.Equal("give", result[0].Verb);
        Assert.Equal(new[] { "blade", "lan" }, result[0].Args);
    }

    [Fact]
    public void Parse_SemicolonChain_ReturnsMultipleEntries()
    {
        var result = CommandInputParser.Parse("get all cor;sac cor;cackle");
        Assert.Equal(3, result.Count);
        Assert.Equal("get", result[0].Verb);
        Assert.Equal("sac", result[1].Verb);
        Assert.Equal("cackle", result[2].Verb);
    }

    [Fact]
    public void Parse_RepeatPrefix_Expands()
    {
        var result = CommandInputParser.Parse("3n");
        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal("n", r.Verb));
    }

    [Fact]
    public void Parse_RepeatPrefix_WithSpeedwalkChain()
    {
        var result = CommandInputParser.Parse("3n;2e;u");
        Assert.Equal(6, result.Count);
        Assert.Equal("n", result[0].Verb);
        Assert.Equal("n", result[2].Verb);
        Assert.Equal("e", result[3].Verb);
        Assert.Equal("u", result[5].Verb);
    }

    [Fact]
    public void Parse_ExceedsMaxChain_Truncates()
    {
        var input = string.Join(";", Enumerable.Repeat("n", 25));
        var result = CommandInputParser.Parse(input);
        Assert.Equal(20, result.Count);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        var result = CommandInputParser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceInput_ReturnsEmpty()
    {
        var result = CommandInputParser.Parse("   ");
        Assert.Empty(result);
    }
}
