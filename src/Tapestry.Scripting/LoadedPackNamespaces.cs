using System.Collections.Generic;

namespace Tapestry.Scripting;

/// <summary>
/// Singleton holder for the live set of loaded pack namespaces. PackLoader fills
/// this during declaration loading; consumers (e.g. <c>WorldAuthoringModule</c>)
/// hold the SAME <see cref="HashSet{T}"/> instance so that a runtime call made
/// after boot (when packs have finished loading) sees the populated set.
///
/// This solves the construction-vs-runtime timing problem: JintRuntime builds its
/// IJintApiModules (including WorldAuthoringModule) possibly before packs finish
/// loading, so a snapshot taken at construction would be empty. By sharing one
/// mutable set, late population is visible to early-constructed consumers.
/// </summary>
public sealed class LoadedPackNamespaces
{
    /// <summary>The live, mutable set of loaded pack namespaces (e.g. "tapestry-core").
    /// Case-insensitive to match <see cref="PackLoader.PackNamespace"/> comparisons.</summary>
    public HashSet<string> Namespaces { get; } = new(System.StringComparer.OrdinalIgnoreCase);

    public void Add(string ns)
    {
        Namespaces.Add(ns);
    }
}
