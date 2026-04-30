namespace Tapestry.Engine.Heartbeat;

public interface IPulseHandler
{
    string Name { get; }
    int Cadence { get; }
    int Priority { get; }
    void Execute(PulseContext context);
}
