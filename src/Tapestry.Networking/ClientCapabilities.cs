namespace Tapestry.Networking;

public enum ColorSupport
{
    None,
    Basic,
    Extended,
    TrueColor
}

public sealed class ClientCapabilities
{
    private static readonly HashSet<string> KnownMudClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "zmud",
        "cmud",
        "mudlet",
        "mushclient",
        "tintin++",
        "tintin",
        "blowtorch",
        "atlantis",
        "potato",
        "beip",
        "kildclient",
        "gnome-mud"
    };

    public string? ClientName { get; }
    public bool SupportsTtype { get; }
    public bool SupportsNaws { get; }
    public bool SupportsGmcp { get; }
    public int WindowWidth { get; }
    public int WindowHeight { get; }
    public ColorSupport ColorSupport { get; }
    public bool UseServerEcho { get; }
    public bool IsMudClient { get; }

    private ClientCapabilities(
        string? clientName,
        bool supportsTtype,
        bool supportsNaws,
        bool supportsGmcp,
        int windowWidth,
        int windowHeight,
        ColorSupport colorSupport,
        bool useServerEcho,
        bool isMudClient)
    {
        ClientName = clientName;
        SupportsTtype = supportsTtype;
        SupportsNaws = supportsNaws;
        SupportsGmcp = supportsGmcp;
        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
        ColorSupport = colorSupport;
        UseServerEcho = useServerEcho;
        IsMudClient = isMudClient;
    }

    public static ClientCapabilities Default { get; } = new ClientCapabilities(
        clientName: null,
        supportsTtype: false,
        supportsNaws: false,
        supportsGmcp: false,
        windowWidth: 80,
        windowHeight: 24,
        colorSupport: ColorSupport.None,
        useServerEcho: true,
        isMudClient: false);

    public static ClientCapabilities FromTimeout()
    {
        return Default;
    }

    public static ClientCapabilities FromNegotiation(string? ttype, int? windowWidth, int? windowHeight, bool gmcpSupported = false)
    {
        var isMudClient = ttype != null && KnownMudClients.Contains(ttype);
        var colorSupport = DeriveColorSupport(ttype, isMudClient);
        var useServerEcho = !isMudClient;
        var supportsTtype = ttype != null;
        var supportsNaws = windowWidth.HasValue && windowHeight.HasValue;
        var width = windowWidth ?? 80;
        var height = windowHeight ?? 24;

        return new ClientCapabilities(
            clientName: ttype,
            supportsTtype: supportsTtype,
            supportsNaws: supportsNaws,
            supportsGmcp: gmcpSupported,
            windowWidth: width,
            windowHeight: height,
            colorSupport: colorSupport,
            useServerEcho: useServerEcho,
            isMudClient: isMudClient);
    }

    private static ColorSupport DeriveColorSupport(string? ttype, bool isMudClient)
    {
        if (ttype == null)
        {
            return ColorSupport.None;
        }

        if (isMudClient)
        {
            return ColorSupport.Extended;
        }

        var upper = ttype.ToUpperInvariant();

        if (upper.Contains("TRUECOLOR"))
        {
            return ColorSupport.TrueColor;
        }

        if (upper.Contains("256COLOR"))
        {
            return ColorSupport.Extended;
        }

        return ColorSupport.Basic;
    }
}
