using Tapestry.Engine.Registration;

namespace Tapestry.Engine.Tests.Registration;

/// <summary>
/// Test helpers for constructing a <see cref="RegistrationPolicy"/> in unit tests that do not
/// exercise cross-pack override edges. <see cref="NoEdgeOracle"/> reports no declared edges, so
/// the policy resolves single registrations and fails on undeclared collisions.
/// </summary>
internal static class TestRegistrationPolicy
{
    internal sealed class NoEdgeOracle : IPackEdgeOracle
    {
        public bool DeclaresEdge(string fromPack, string toPack) => false;
    }

    internal static RegistrationPolicy Create() => new(new NoEdgeOracle());
}
