using Tapestry.Scripting.Modules;
using Xunit;

namespace Tapestry.Scripting.Tests.Modules;

public class WorldModuleVersionTests
{
    [Fact]
    public void BuildInfo_ReturnsEngineVersion_FromEnvironment()
    {
        var previousValue = Environment.GetEnvironmentVariable("ENGINE_BUILD_VERSION");
        try
        {
            Environment.SetEnvironmentVariable("ENGINE_BUILD_VERSION", "0.1.2");

            var buildInfo = WorldModule.GetBuildInfo();

            Assert.Equal("0.1.2", buildInfo.EngineVersion);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENGINE_BUILD_VERSION", previousValue);
        }
    }

    [Fact]
    public void BuildInfo_DefaultsToDev_WhenEnvNotSet()
    {
        var previousValue = Environment.GetEnvironmentVariable("ENGINE_BUILD_VERSION");
        try
        {
            Environment.SetEnvironmentVariable("ENGINE_BUILD_VERSION", null);

            var buildInfo = WorldModule.GetBuildInfo();

            Assert.Equal("dev", buildInfo.EngineVersion);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ENGINE_BUILD_VERSION", previousValue);
        }
    }
}
