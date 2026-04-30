using System.Diagnostics;

namespace Tapestry.Engine;

public static class TapestryTracing
{
    public const string SourceName = "Tapestry";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");
}
