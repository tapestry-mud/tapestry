namespace Tapestry.Engine.Registration;

/// <summary>
/// Abstraction over the pack dependency graph so Tapestry.Engine can consult the
/// declared-dependency edge without referencing Tapestry.Scripting (where
/// PackDependencyGraph lives). Implemented by PackDependencyGraph.
/// </summary>
public interface IPackEdgeOracle
{
    /// <summary>True if <paramref name="fromPack"/> declares a dependency on <paramref name="toPack"/>.</summary>
    bool DeclaresEdge(string fromPack, string toPack);
}
