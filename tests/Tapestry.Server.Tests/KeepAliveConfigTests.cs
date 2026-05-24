using FluentAssertions;
using Tapestry.Data;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Server.Tests;

public class KeepAliveConfigTests
{
    private static ServerConfig Deserialize(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<ServerConfig>(yaml);
    }

    [Fact]
    public void KeepAlive_defaults_enabled_with_sensible_windows()
    {
        var config = Deserialize("networking: {}");

        config.Networking.KeepAlive.Enabled.Should().BeTrue();
        config.Networking.KeepAlive.IdleSeconds.Should().Be(60);
        config.Networking.KeepAlive.IntervalSeconds.Should().Be(15);
        config.Networking.KeepAlive.RetryCount.Should().Be(4);
    }

    [Fact]
    public void KeepAlive_deserializes_underscored_yaml_keys()
    {
        const string yaml = """
            networking:
              keep_alive:
                enabled: false
                idle_seconds: 30
                interval_seconds: 10
                retry_count: 3
            """;

        var config = Deserialize(yaml);

        config.Networking.KeepAlive.Enabled.Should().BeFalse();
        config.Networking.KeepAlive.IdleSeconds.Should().Be(30);
        config.Networking.KeepAlive.IntervalSeconds.Should().Be(10);
        config.Networking.KeepAlive.RetryCount.Should().Be(3);
    }
}
