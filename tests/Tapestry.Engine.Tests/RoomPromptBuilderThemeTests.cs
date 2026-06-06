using Tapestry.Authoring;
using Tapestry.Engine.Authoring;
using Xunit;

namespace Tapestry.Engine.Tests;

public class RoomPromptBuilderThemeTests
{
    [Fact]
    public void Build_WithAreaTheme_EmitsAreaNameAndTheme()
    {
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var ctx = new RoomData
        {
            Id = "road-to-tar-valon-1", Area = "road-to-tar-valon", AreaName = "Road to Tar Valon",
            AreaTheme = "Grim pilgrimage under a watchful Tower.", Name = "Track"
        };
        var (_, user) = builder.Build("description", ctx, null);

        Assert.Contains("Area: Road to Tar Valon. Theme: Grim pilgrimage under a watchful Tower.", user);
    }

    [Fact]
    public void Build_WithoutTheme_FallsBackToAreaIdLine()
    {
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var ctx = new RoomData { Id = "r", Area = "road-to-tar-valon", Name = "Track" }; // no AreaName/Theme
        var (_, user) = builder.Build("description", ctx, null);
        Assert.Contains("Area: road-to-tar-valon.", user);
    }
}
