using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tapestry.Scripting.Tests;

public class RuntimeNamespaceStoreTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tapestry-rnstest-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Register_adds_to_the_live_set_and_persists_to_the_marker()
    {
        var dir = TempDir();
        var ns = new LoadedPackNamespaces();
        var store = new RuntimeNamespaceStore(dir, ns, NullLogger<RuntimeNamespaceStore>.Instance);

        store.Register("oracle-run");

        ns.Namespaces.Should().Contain("oracle-run");
        var marker = Path.Combine(dir, "runtime-namespaces.txt");
        File.Exists(marker).Should().BeTrue();
        File.ReadAllText(marker).Should().Contain("oracle-run");
    }

    [Fact]
    public void Register_is_idempotent_and_does_not_duplicate_marker_lines()
    {
        var dir = TempDir();
        var ns = new LoadedPackNamespaces();
        var store = new RuntimeNamespaceStore(dir, ns, NullLogger<RuntimeNamespaceStore>.Instance);

        store.Register("oracle-run");
        store.Register("oracle-run");

        var lines = File.ReadAllLines(Path.Combine(dir, "runtime-namespaces.txt"));
        lines.Should().ContainSingle(l => l.Trim() == "oracle-run");
    }

    [Fact]
    public void LoadAtBoot_re_registers_persisted_namespaces_into_a_fresh_set()
    {
        var dir = TempDir();
        // Session 1: register two namespaces.
        var session1 = new LoadedPackNamespaces();
        new RuntimeNamespaceStore(dir, session1, NullLogger<RuntimeNamespaceStore>.Instance).Register("oracle-run");
        new RuntimeNamespaceStore(dir, session1, NullLogger<RuntimeNamespaceStore>.Instance).Register("solo-foo");

        // Session 2 (reboot): a fresh empty set re-registers from the marker.
        var session2 = new LoadedPackNamespaces();
        var reboot = new RuntimeNamespaceStore(dir, session2, NullLogger<RuntimeNamespaceStore>.Instance);
        session2.Namespaces.Should().BeEmpty();

        reboot.LoadAtBoot();

        session2.Namespaces.Should().Contain("oracle-run");
        session2.Namespaces.Should().Contain("solo-foo");
    }

    [Fact]
    public void LoadAtBoot_is_a_noop_when_the_marker_is_absent()
    {
        var dir = TempDir();
        var ns = new LoadedPackNamespaces();
        var store = new RuntimeNamespaceStore(dir, ns, NullLogger<RuntimeNamespaceStore>.Instance);

        store.LoadAtBoot(); // no marker yet

        ns.Namespaces.Should().BeEmpty();
    }
}
