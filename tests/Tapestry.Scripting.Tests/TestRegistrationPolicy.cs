using Tapestry.Engine.Registration;

namespace Tapestry.Scripting.Tests;

/// <summary>
/// Test helper for constructing a <see cref="RegistrationPolicy"/> in scripting tests that wire a
/// <c>GameLoop</c> as a dependency but never seal/tick. <see cref="NoEdgeOracle"/> reports no
/// declared edges.
/// </summary>
internal static class TestRegistrationPolicy
{
    internal sealed class NoEdgeOracle : IPackEdgeOracle
    {
        public bool DeclaresEdge(string fromPack, string toPack) => false;
    }

    internal static RegistrationPolicy Create() => new(new NoEdgeOracle());
}
