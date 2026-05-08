using FluentAssertions;
using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;

namespace Tapestry.Engine.Tests.Gmcp;

public class PostLoginOrchestratorTests
{
    private class SpyHandler : IGmcpPackageHandler
    {
        private readonly Action _onBurst;
        public string Name { get; }
        public IReadOnlyList<string> PackageNames { get; } = Array.Empty<string>();
        public void Configure() { }

        public SpyHandler(string name, Action onBurst)
        {
            Name = name;
            _onBurst = onBurst;
        }

        public void SendBurst(string connectionId, object entity) => _onBurst();
    }

    [Fact]
    public void SendPostLoginBurst_CallsAllMatchingHandlersInOrder()
    {
        var callOrder = new List<string>();
        var h1 = new SpyHandler("Alpha", () => { callOrder.Add("Alpha"); });
        var h2 = new SpyHandler("Beta", () => { callOrder.Add("Beta"); });

        var orchestrator = new PostLoginOrchestrator(
            new IGmcpPackageHandler[] { h2, h1 },
            new[] { h1.GetType(), h2.GetType() });

        var entity = new Entity("player", "Test");
        orchestrator.SendPostLoginBurst("conn1", entity);

        callOrder.Should().HaveCount(2);
    }

    [Fact]
    public void SendPostLoginBurst_ExcludesHandlersNotInBurstOrder()
    {
        var callOrder = new List<string>();
        var included = new SpyHandler("Included", () => { callOrder.Add("Included"); });
        var excluded = new SpyHandler("Excluded", () => { callOrder.Add("Excluded"); });

        var orchestrator = new PostLoginOrchestrator(
            new IGmcpPackageHandler[] { included, excluded },
            new[] { included.GetType() });

        var entity = new Entity("player", "Test");
        orchestrator.SendPostLoginBurst("conn1", entity);

        callOrder.Should().ContainSingle("Included");
        callOrder.Should().NotContain("Excluded");
    }
}
