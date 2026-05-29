namespace Tapestry.Scripting.Interop;

/// <summary>
/// Boot-lived collection of recorded interop call-sites. Populated by <c>PackLoader</c> during
/// script loading and drained by <c>PackValidator</c> for static interop resolution.
/// Survives a single boot; cleared per load cycle if hot-reload is ever added.
/// </summary>
public sealed class InteropCallSiteRegistry
{
    private readonly List<InteropCallSite> _sites = new();

    public void Record(InteropCallSite site) => _sites.Add(site);

    public IReadOnlyList<InteropCallSite> All => _sites;

    public void Clear() => _sites.Clear();
}
