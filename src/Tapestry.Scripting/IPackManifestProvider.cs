using Tapestry.Shared;

namespace Tapestry.Scripting;

public interface IPackManifestProvider
{
    IReadOnlyList<PackManifest> LoadedPacks { get; }
}
