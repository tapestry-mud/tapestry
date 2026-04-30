namespace Tapestry.Networking;

public static class TelnetProtocolConstants
{
    public const byte IAC  = 255; // Interpret As Command
    public const byte SB   = 250; // Subnegotiation begin
    public const byte SE   = 240; // Subnegotiation end
    public const byte WILL = 251;
    public const byte WONT = 252;
    public const byte DO   = 253;
    public const byte DONT = 254;

    public const byte OPT_ECHO  = 1;
    public const byte OPT_TTYPE = 24;
    public const byte OPT_NAWS  = 31;
    public const byte OPT_MSSP  = 70;
    public const byte OPT_GMCP  = 201;

    public const byte MSSP_VAR = 1;
    public const byte MSSP_VAL = 2;
}
