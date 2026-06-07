using Tapestry.Authoring;
using Tapestry.Engine.Authoring;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AreaPromptBuilderTests
{
    private static AreaData Sample()
    {
        return new AreaData
        {
            Id = "road-to-tar-valon", Name = "Road to Tar Valon",
            Short = "A pilgrim road north.", Theme = "Grim pilgrimage.",
            LevelRange = new[] { 5, 12 },
            Rooms = { new AreaRoomSample { Name = "Dusty Track", DescriptionSnippet = "A long dusty track." } }
        };
    }

    [Fact]
    public void Build_Theme_IncludesSiblingFields_RoomSample_AndAreaTaskLine()
    {
        var builder = new AreaPromptBuilder(RecommendPromptConfig.Default);
        var (system, user) = builder.Build("theme", Sample(), null);

        // Area authoring uses the region-voice system prompt, NOT the room prompt.
        Assert.Contains("AREA", system);
        Assert.DoesNotContain("Second person", system);
        Assert.Contains("Road to Tar Valon", user);
        Assert.Contains("Dusty Track", user);
        Assert.Contains("5", user);
        Assert.Contains("theme", user.ToLowerInvariant());
    }

    [Fact]
    public void Build_UsesAreaSystemPrompt_NotRoomSystemPrompt()
    {
        var builder = new AreaPromptBuilder(RecommendPromptConfig.Default);
        var (system, _) = builder.Build("description", Sample(), null);
        Assert.Equal(RecommendPromptConfig.DefaultAreaSystemPrompt, system);
        Assert.NotEqual(RecommendPromptConfig.DefaultSystemPrompt, system);
    }

    [Fact]
    public void Build_Hint_IsAppended()
    {
        var builder = new AreaPromptBuilder(RecommendPromptConfig.Default);
        var (_, user) = builder.Build("short", Sample(), "make it ominous");
        Assert.Contains("ominous", user);
    }
}
