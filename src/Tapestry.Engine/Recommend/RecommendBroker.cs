using System.Threading.Tasks;

namespace Tapestry.Engine.Recommend;

/// <summary>Holds 0..1 recommend provider. Aggregates a single availability flag from three
/// layers: config (what got bound), provider capability (<see cref="IRecommendProvider.IsEnabled"/>),
/// and a runtime admin gate (<see cref="SetEnabled"/>). Consumers read only <see cref="IsEnabled"/>.</summary>
public sealed class RecommendBroker
{
    private IRecommendProvider? _provider;
    private bool _adminEnabled = true;

    public bool HasProvider => _provider != null;

    /// <summary>The single flag all consumers key on: a provider is bound, it reports itself
    /// enabled, and an admin hasn't switched it off at runtime.</summary>
    public bool IsEnabled => _adminEnabled && _provider?.IsEnabled == true;

    public void Register(IRecommendProvider provider)
    {
        _provider = provider; // last-wins; v1 binds exactly one
    }

    /// <summary>Runtime on/off, independent of config/capability (e.g. an admin command).</summary>
    public void SetEnabled(bool enabled)
    {
        _adminEnabled = enabled;
    }

    public Task<RecommendResult> RecommendAsync(RecommendRequest request)
    {
        if (!IsEnabled)
        {
            return Task.FromResult(RecommendResult.Empty);
        }
        return _provider!.RecommendAsync(request);
    }
}
