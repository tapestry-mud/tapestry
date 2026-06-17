using Microsoft.AspNetCore.Mvc.Testing;

namespace Tapestry.Server.Tests.Auth;

public sealed class AuthTestApp : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _tempDir;

    public AuthTestApp(bool preAuthEnabled = true)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tapestry-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "saves"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "connections"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "areas"));

        // YAML needs forward slashes (backslashes are escape chars in some YAML parsers)
        var tempDir = _tempDir.Replace('\\', '/');
        var enabled = preAuthEnabled ? "true" : "false";
        var configPath = Path.Combine(_tempDir, "server.yaml");
        File.WriteAllText(configPath, $"""
            server:
              telnet_port: 0
              websocket_port: 0
            packs: []
            pre_auth:
              enabled: {enabled}
            persistence:
              save_path: {tempDir}/saves
              connections_path: {tempDir}/connections
              rooms_path: {tempDir}/areas
            """);

        Environment.SetEnvironmentVariable("TAPESTRY_CONFIG", configPath);

        // The auth host boots with no content packs (packs: []), so the engine's built-in
        // commands (badinput, resetpassword, save) have no help topics. The help-coverage
        // gate is strict by default and would fault host startup; these tests do not exercise
        // help, so run the gates lenient (warn, never throw). Prod sets no env and stays strict.
        Environment.SetEnvironmentVariable("TAPESTRY_HELP_GATES", "lenient");
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable("TAPESTRY_CONFIG", null);
        Environment.SetEnvironmentVariable("TAPESTRY_HELP_GATES", null);
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
        base.Dispose(disposing);
    }
}
