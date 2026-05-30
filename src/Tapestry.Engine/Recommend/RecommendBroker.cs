using System.Threading.Tasks;

namespace Tapestry.Engine.Recommend;

/// <summary>Holds 0..1 recommend provider. No-op (empty list) when none registered.
/// v1 binds a single provider; named/multi providers can come later without caller changes.</summary>
public sealed class RecommendBroker
{
    private IRecommendProvider? _provider;

    public bool HasProvider => _provider != null;

    public void Register(IRecommendProvider provider)
    {
        _provider = provider; // last-wins; v1 binds exactly one
    }

    public Task<RecommendResult> RecommendAsync(RecommendRequest request)
    {
        if (_provider == null)
        {
            return Task.FromResult(RecommendResult.Empty);
        }
        return _provider.RecommendAsync(request);
    }
}
