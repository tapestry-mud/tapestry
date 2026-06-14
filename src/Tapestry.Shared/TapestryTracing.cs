using System.Diagnostics;

namespace Tapestry.Shared;

// Lives in Tapestry.Shared (not Engine) so the transport layer (Tapestry.Networking)
// can emit spans on the SAME ActivitySource instance the rest of the engine uses.
// A second ActivitySource with the same name is not reliably captured by the OTel
// AddSource listener, so all spans must come from this one instance.
public static class TapestryTracing
{
    public const string SourceName = "Tapestry";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");
}
