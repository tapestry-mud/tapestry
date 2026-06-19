using Tapestry.Engine.Registration;

namespace Tapestry.Scripting.Tests;

internal sealed class FakeEdges : IPackEdgeOracle
{
    public HashSet<(string, string)> Edges = new();
    public bool DeclaresEdge(string from, string to) => Edges.Contains((from, to));
}
