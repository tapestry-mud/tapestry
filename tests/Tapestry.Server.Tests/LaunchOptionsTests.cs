using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Tapestry.Server;

namespace Tapestry.Server.Tests;

public class LaunchOptionsTests
{
    private static IConfiguration FromArgs(params string[] args) =>
        new ConfigurationBuilder().AddCommandLine(args).Build();

    [Fact]
    public void Defaults_to_server_yaml_and_null_packs_when_no_args()
    {
        var (configPath, packsDir) = LaunchOptions.Resolve(FromArgs());

        configPath.Should().Be("server.yaml");
        packsDir.Should().BeNull();
    }

    [Fact]
    public void Reads_the_config_flag()
    {
        var (configPath, _) = LaunchOptions.Resolve(FromArgs("--config", "/etc/tapestry/server.yaml"));

        configPath.Should().Be("/etc/tapestry/server.yaml");
    }

    [Fact]
    public void Reads_the_packs_flag()
    {
        var (_, packsDir) = LaunchOptions.Resolve(FromArgs("--packs", "/srv/packs"));

        packsDir.Should().Be("/srv/packs");
    }

    [Fact]
    public void Reads_both_flags_together()
    {
        var (configPath, packsDir) =
            LaunchOptions.Resolve(FromArgs("--packs", "/srv/packs", "--config", "/c.yaml"));

        configPath.Should().Be("/c.yaml");
        packsDir.Should().Be("/srv/packs");
    }
}
