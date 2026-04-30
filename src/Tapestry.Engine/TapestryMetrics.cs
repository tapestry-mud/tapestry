using System.Diagnostics.Metrics;

namespace Tapestry.Engine;

public class TapestryMetrics
{
    public const string MeterName = "Tapestry";

    private readonly Meter _meter;

    public Histogram<double> TickDuration { get; }
    public Counter<long> CommandsProcessed { get; }
    public Counter<long> EventsProcessed { get; }
    public UpDownCounter<long> ActiveConnections { get; }
    public Histogram<double> CommandDuration { get; }
    public Histogram<long> InputQueueDepth { get; }
    public Histogram<double> SessionDuration { get; }

    public TapestryMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        TickDuration = _meter.CreateHistogram<double>(
            "tapestry.tick.duration_ms",
            unit: "ms",
            description: "Time spent in each tick phase");

        CommandsProcessed = _meter.CreateCounter<long>(
            "tapestry.tick.commands_processed",
            description: "Commands processed per tick");

        EventsProcessed = _meter.CreateCounter<long>(
            "tapestry.tick.events_processed",
            description: "System events processed per tick");

        ActiveConnections = _meter.CreateUpDownCounter<long>(
            "tapestry.connections.active",
            description: "Current active connections");

        CommandDuration = _meter.CreateHistogram<double>(
            "tapestry.command.duration_ms",
            unit: "ms",
            description: "Execution time per command handler");

        InputQueueDepth = _meter.CreateHistogram<long>(
            "tapestry.input_queue.depth",
            description: "Queue depth sampled each tick");

        SessionDuration = _meter.CreateHistogram<double>(
            "tapestry.session.duration_s",
            unit: "s",
            description: "Total session duration on disconnect");
    }
}
