using FluentAssertions;
using Tapestry.Data;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Server.Tests;

public class NetworkingSectionConfigTests
{
    private static ServerConfig Deserialize(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<ServerConfig>(yaml);
    }

    [Fact]
    public void TrustedProxies_defaults_to_empty_list()
    {
        var config = Deserialize("networking: {}");
        config.Networking.TrustedProxies.Should().BeEmpty();
    }

    [Fact]
    public void TrustedProxies_deserializes_yaml_key_trusted_proxies()
    {
        const string yaml = """
            networking:
              trusted_proxies:
                - 172.18.0.0/16
                - 127.0.0.1/32
            """;

        var config = Deserialize(yaml);
        config.Networking.TrustedProxies.Should().Equal("172.18.0.0/16", "127.0.0.1/32");
    }
}
