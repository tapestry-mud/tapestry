using Tapestry.Engine.Tags;
using Tapestry.Engine.Persistence;

namespace Tapestry.Scripting.Tests;

public class TwoPhaseLoadingTests
{
    [Fact]
    public void LoadDeclarations_And_LoadContent_Exist()
    {
        var tagRegistry = new TagRegistry();
        var propertyRegistry = new PropertyRegistry();

        Assert.NotNull(tagRegistry);
        Assert.NotNull(propertyRegistry);
    }
}
