using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Tapestry.Scripting;

/// <summary>
/// Persists namespaces created at RUNTIME (e.g. the solo-oracle destination pack made by
/// <c>tapestry.authoring.createPack</c>) to a small marker file under the writable data
/// directory, and re-registers them into <see cref="LoadedPackNamespaces"/> at boot.
///
/// Why this exists: a runtime-created pack's namespace must survive a reboot so that
/// continued lazy-mint (<c>createRoom</c>, which gates on the loaded-namespace set) keeps
/// working. The old mechanism wrote a pack.yaml scaffold into the packs directory and let
/// PackLoader re-register the namespace on the next boot. That fails in the docker
/// deployment: the engine runs as a non-root uid and the packs directory is bind-mounted
/// read-only-to-the-engine (owned by the deploy user), so the scaffold write throws
/// "Access denied". The data directory, by contrast, is owned by the engine process (it
/// already writes player saves and area side-cars there), so a marker there is always
/// writable. The generated CONTENT (areas/rooms/items/oracle tables) already persists as
/// data side-cars loaded by the Authored*Loaders independently of any pack, so the
/// namespace registration is the only thing the scaffold provided - and this replaces it.
/// </summary>
public sealed class RuntimeNamespaceStore
{
    private readonly string _markerPath;
    private readonly LoadedPackNamespaces _namespaces;
    private readonly ILogger<RuntimeNamespaceStore> _logger;
    private readonly HashSet<string> _runtime = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeNamespaceStore(string dataRoot, LoadedPackNamespaces namespaces, ILogger<RuntimeNamespaceStore> logger)
    {
        _markerPath = Path.Combine(dataRoot, "runtime-namespaces.txt");
        _namespaces = namespaces;
        _logger = logger;
    }

    /// <summary>Register a runtime-created namespace: add it to the live loaded set and, if it
    /// is new, append it to the marker so a future boot re-registers it. Idempotent.</summary>
    public void Register(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
        {
            return;
        }
        _runtime.Add(ns);
        if (!_namespaces.Namespaces.Add(ns))
        {
            return; // already known - already in the marker, nothing to persist
        }
        try
        {
            var dir = Path.GetDirectoryName(_markerPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(_markerPath, ns + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The namespace is still live in-memory for this session; only persistence
            // across reboot is lost. Log rather than crash the runtime call.
            _logger.LogWarning("RuntimeNamespaceStore: could not persist namespace '{Ns}' to {Path}: {Msg}", ns, _markerPath, ex.Message);
        }
    }

    /// <summary>Re-register every persisted runtime namespace at boot. Safe when the marker is
    /// absent (fresh world). Call before the server accepts movement (lazy-mint).</summary>
    public void LoadAtBoot()
    {
        if (!File.Exists(_markerPath))
        {
            return;
        }
        var added = 0;
        foreach (var line in File.ReadAllLines(_markerPath))
        {
            var ns = line.Trim();
            if (ns.Length == 0)
            {
                continue;
            }
            _runtime.Add(ns);
            if (_namespaces.Namespaces.Add(ns))
            {
                added++;
            }
        }
        if (added > 0)
        {
            _logger.LogInformation("Re-registered {Count} runtime namespace(s) from {Path}", added, _markerPath);
        }
    }

    /// <summary>True when the namespace was created at runtime (this session) or restored
    /// from the marker at boot. PackValidator treats these as LENIENT: a runtime namespace
    /// has no manifest to carry <c>validation:</c> (docker cannot write the packs-dir
    /// scaffold, and `server.yaml packs:` whitelists it out even when written), and its
    /// content is engine-written side-cars, so strict validation would crash the boot on
    /// any pack-declared property riding a generated room.</summary>
    public bool IsRuntimeNamespace(string ns) => _runtime.Contains(ns);
}
