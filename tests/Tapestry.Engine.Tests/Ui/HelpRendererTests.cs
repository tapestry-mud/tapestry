using Tapestry.Engine.Ui;
using Tapestry.Shared.Help;
using Xunit;

namespace Tapestry.Engine.Tests.Ui;

public class HelpRendererTests
{
    [Fact]
    public void RenderTopic_ContainsTitle()
    {
        var topic = new HelpTopic { Title = "Combat Basics", Brief = "Fight stuff.", Body = "Body here." };
        var output = HelpRenderer.RenderTopic(topic, 60);
        Assert.Contains("Combat Basics", output);
    }

    [Fact]
    public void RenderTopic_ContainsBrief()
    {
        var topic = new HelpTopic { Title = "X", Brief = "Fight stuff.", Body = "B." };
        var output = HelpRenderer.RenderTopic(topic, 60);
        Assert.Contains("Fight stuff.", output);
    }

    [Fact]
    public void RenderTopic_ContainsSyntaxLines()
    {
        var topic = new HelpTopic { Title = "X", Brief = "B.", Body = "Body.", Syntax = new() { "kill [target]", "flee" } };
        var output = HelpRenderer.RenderTopic(topic, 60);
        Assert.Contains("kill [target]", output);
        Assert.Contains("flee", output);
    }

    [Fact]
    public void RenderTopic_ContainsSeeAlso()
    {
        var topic = new HelpTopic { Title = "X", Brief = "B.", Body = "Body.", SeeAlso = new() { "combat-advanced" } };
        var output = HelpRenderer.RenderTopic(topic, 60);
        Assert.Contains("combat-advanced", output);
    }

    [Fact]
    public void RenderDisambiguation_ContainsAllMatches()
    {
        var matches = new List<HelpTopicSummary>
        {
            new() { Id = "combat-basics", Title = "Combat Basics", Brief = "..." },
            new() { Id = "combat-advanced", Title = "Advanced Combat", Brief = "..." }
        };
        var output = HelpRenderer.RenderDisambiguation("combat", matches, 60);
        Assert.Contains("combat-basics", output);
        Assert.Contains("combat-advanced", output);
    }

    [Fact]
    public void RenderDisambiguation_ContainsMultipleMatchesLabel()
    {
        var matches = new List<HelpTopicSummary>
        {
            new() { Id = "a", Title = "A", Brief = "." },
            new() { Id = "b", Title = "B", Brief = "." }
        };
        var output = HelpRenderer.RenderDisambiguation("x", matches, 60);
        Assert.Contains("Multiple matches", output);
    }
}
