using Tapestry.Shared;

namespace Tapestry.Engine.Login;

public class LoginContext
{
    public string ConnectionId { get; }
    public IConnection Connection { get; }
    public LoginPhase Phase { get; set; }
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;
    public CancellationTokenSource PhaseCts { get; set; } = new();

    public LoginContext(string connectionId, IConnection connection, LoginPhase phase = LoginPhase.Connected)
    {
        ConnectionId = connectionId;
        Connection = connection;
        Phase = phase;
    }
}
