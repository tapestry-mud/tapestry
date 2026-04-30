using System.Text.Json;

namespace Tapestry.Shared;

public interface IGmcpHandler
{
    bool GmcpActive { get; }
    void Send(string package, object payload);
    bool SupportsPackage(string package);
    Action<string, JsonElement>? OnGmcpMessage { get; set; }
}
