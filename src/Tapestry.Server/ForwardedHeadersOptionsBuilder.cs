using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
// Disambiguates from Microsoft.AspNetCore.HttpOverrides.IPNetwork (obsolete in .NET 10).
using IPNetwork = System.Net.IPNetwork;

namespace Tapestry.Server;

internal static class ForwardedHeadersOptionsBuilder
{
    internal static ForwardedHeadersOptions Build(List<string> trustedProxies)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var cidr in trustedProxies)
        {
            options.KnownIPNetworks.Add(ParseCidr(cidr));
        }

        return options;
    }

    private static IPNetwork ParseCidr(string cidr)
    {
        var parts = cidr.Split('/', 2);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var address)
            || !int.TryParse(parts[1], out var prefixLength))
        {
            throw new InvalidOperationException(
                $"Malformed trusted_proxies entry: '{cidr}'. Expected CIDR notation, e.g. 172.18.0.0/16.");
        }

        var maxPrefix = address.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            throw new InvalidOperationException(
                $"Malformed trusted_proxies entry: '{cidr}'. Prefix length {prefixLength} out of range for address family.");
        }

        return new IPNetwork(address, prefixLength);
    }
}
