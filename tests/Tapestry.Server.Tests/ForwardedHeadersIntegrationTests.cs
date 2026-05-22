using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace Tapestry.Server.Tests;

public class ForwardedHeadersIntegrationTests : IAsyncLifetime
{
    // Host configured with 127.0.0.1/32 as the trusted proxy (loopback = TestServer's "peer")
    private IHost _trustedHost = null!;

    // Host with no trusted_proxies → middleware not registered
    private IHost _directHost = null!;

    public async Task InitializeAsync()
    {
        _trustedHost = await BuildHost(trustedProxies: new List<string> { "127.0.0.1/32" });
        _directHost  = await BuildHost(trustedProxies: new List<string>());
    }

    public async Task DisposeAsync()
    {
        await _trustedHost.StopAsync();
        await _directHost.StopAsync();
        _trustedHost.Dispose();
        _directHost.Dispose();
    }

    private static async Task<IHost> BuildHost(List<string> trustedProxies)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    // TestServer has no real TCP socket, so Connection.RemoteIpAddress is
                    // null by default. Inject a deterministic loopback peer for BOTH hosts:
                    // - trusted host: loopback is inside the 127.0.0.1/32 trusted CIDR, so
                    //   ForwardedHeaders accepts the peer and rewrites from XFF.
                    // - direct host: no middleware registered, so the echo returns this peer
                    //   unchanged, proving XFF was ignored.
                    // Must run BEFORE UseForwardedHeaders.
                    app.Use(async (ctx, next) =>
                    {
                        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
                        await next();
                    });

                    if (trustedProxies.Count > 0)
                    {
                        app.UseForwardedHeaders(
                            ForwardedHeadersOptionsBuilder.Build(trustedProxies));
                    }

                    app.Run(async ctx =>
                    {
                        await ctx.Response.WriteAsync(
                            ctx.Connection.RemoteIpAddress?.ToString() ?? "null");
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    [Fact]
    public async Task Trusted_proxy_XFF_rewrites_RemoteIpAddress_to_real_client()
    {
        var client = _trustedHost.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "1.2.3.4");

        var response = await client.GetAsync("/");
        var ip = await response.Content.ReadAsStringAsync();

        ip.Should().Be("1.2.3.4");
    }

    [Fact]
    public async Task Spoofed_prepend_is_discarded_rightmost_hop_wins()
    {
        // Simulate: malicious client sent "8.8.8.8", Caddy appended the real peer "1.2.3.4"
        var client = _trustedHost.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "8.8.8.8, 1.2.3.4");

        var response = await client.GetAsync("/");
        var ip = await response.Content.ReadAsStringAsync();

        // ForwardLimit=1 means only the rightmost Caddy-added hop is accepted
        ip.Should().Be("1.2.3.4");
    }

    [Fact]
    public async Task Empty_trusted_proxies_ignores_XFF_returns_peer_ip()
    {
        var client = _directHost.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "9.9.9.9");

        var response = await client.GetAsync("/");
        var ip = await response.Content.ReadAsStringAsync();

        // Middleware not registered → XFF ignored → the injected loopback peer is returned
        ip.Should().NotBe("9.9.9.9");
        ip.Should().Be("127.0.0.1");
    }
}
