using FluentAssertions;
using Tapestry.Data;

namespace Tapestry.Server.Tests;

public class ServerConfigPacksDirectoryTests
{
    [Fact]
    public void ResolvedPacksDirectory_defaults_under_the_base_directory()
    {
        new ServerConfig().ResolvedPacksDirectory
            .Should().Be(Path.Combine(AppContext.BaseDirectory, "packs"));
    }

    [Fact]
    public void ResolvedPacksDirectory_uses_the_override_when_set()
    {
        new ServerConfig { PacksDirectory = "/srv/packs" }.ResolvedPacksDirectory
            .Should().Be("/srv/packs");
    }
}
