using Tapestry.Engine.Authoring;
using Tapestry.Engine.Recommend;
using Xunit;

namespace Tapestry.Engine.Tests;

public class RecommendContextTests
{
    [Fact]
    public void RoomData_IsRecommendContext()
    {
        IRecommendContext ctx = new RoomData { Id = "r" };
        var req = new RecommendRequest("description", ctx, null);
        Assert.IsType<RoomData>(req.Context);
    }
}
