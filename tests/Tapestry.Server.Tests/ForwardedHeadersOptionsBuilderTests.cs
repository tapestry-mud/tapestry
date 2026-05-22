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

public class ForwardedHeadersOptionsBuilderParserTests
{
    [Fact]
    public void Build_with_valid_ipv4_cidr_populates_KnownNetworks()
    {
        var options = ForwardedHeadersOptionsBuilder.Build(new List<string> { "172.18.0.0/16" });

        options.KnownIPNetworks.Should().ContainSingle(n =>
            n.BaseAddress.ToString() == "172.18.0.0" && n.PrefixLength == 16);
    }

    [Fact]
    public void Build_with_multiple_cidrs_populates_all_networks()
    {
        var options = ForwardedHeadersOptionsBuilder.Build(
            new List<string> { "172.18.0.0/16", "127.0.0.1/32" });

        options.KnownIPNetworks.Should().HaveCount(2);
    }

    [Fact]
    public void Build_clears_defaults_so_KnownNetworks_contains_exactly_configured_entries()
    {
        var options = ForwardedHeadersOptionsBuilder.Build(new List<string> { "172.18.0.0/16" });

        // If Clear() was skipped, ASP.NET's default loopback entries would inflate the count.
        options.KnownIPNetworks.Should().HaveCount(1);
    }

    [Fact]
    public void Build_clears_KnownProxies()
    {
        var options = ForwardedHeadersOptionsBuilder.Build(new List<string> { "172.18.0.0/16" });
        options.KnownProxies.Should().BeEmpty();
    }

    [Fact]
    public void Build_sets_ForwardLimit_to_one()
    {
        var options = ForwardedHeadersOptionsBuilder.Build(new List<string> { "172.18.0.0/16" });
        options.ForwardLimit.Should().Be(1);
    }

    [Fact]
    public void Build_enables_XForwardedFor_and_XForwardedProto_flags()
    {
        var options = ForwardedHeadersOptionsBuilder.Build(new List<string> { "172.18.0.0/16" });

        options.ForwardedHeaders.Should().HaveFlag(
            Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor);
        options.ForwardedHeaders.Should().HaveFlag(
            Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("172.18.0.0/99")]
    [InlineData("172.18.0.0")]
    [InlineData("not-an-ip/16")]
    public void Build_throws_on_malformed_cidr(string bad)
    {
        var act = () => ForwardedHeadersOptionsBuilder.Build(new List<string> { bad });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{bad}*");
    }
}
