using Microsoft.Extensions.Configuration;

namespace Tapestry.Server;

/// <summary>
/// Resolves launch-time options from process arguments. Reads the
/// <c>--config &lt;path&gt;</c> and <c>--packs &lt;dir&gt;</c> flags via the
/// command-line configuration provider (no extra dependency).
/// </summary>
public static class LaunchOptions
{
    public const string DefaultConfigPath = "server.yaml";

    /// <summary>
    /// Returns the config file path (defaulting to <c>server.yaml</c>) and an
    /// optional packs directory override (null when not supplied).
    /// </summary>
    public static (string ConfigPath, string? PacksDirectory) Resolve(IConfiguration config)
    {
        var configPath = config["config"];
        var packs = config["packs"];
        return (
            string.IsNullOrWhiteSpace(configPath) ? DefaultConfigPath : configPath,
            string.IsNullOrWhiteSpace(packs) ? null : packs
        );
    }
}
