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
    public Histogram<double> HandlerWallMs { get; }
    public Histogram<double> HandlerCpuMs { get; }
    public Histogram<double> MobAiPhaseMs { get; }
    public Histogram<long> InputQueueDepth { get; }
    public Histogram<double> SessionDuration { get; }
    public Counter<long> FloodCommandsDropped { get; }
    public Counter<long> FloodDisconnects { get; }
    public Counter<long> BadInputTotal { get; }
    public UpDownCounter<long> LinkDeadActive { get; }
    public Counter<long> LinkDeadReconnected { get; }
    public Counter<long> LinkDeadExpired { get; }

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

        HandlerWallMs = _meter.CreateHistogram<double>(
            "tapestry.tick.handler_wall_ms",
            unit: "ms",
            description: "Per-handler wall-clock time per tick, tagged by handler");

        HandlerCpuMs = _meter.CreateHistogram<double>(
            "tapestry.tick.handler_cpu_ms",
            unit: "ms",
            description: "Per-handler thread CPU time per tick, tagged by handler");

        MobAiPhaseMs = _meter.CreateHistogram<double>(
            "tapestry.mob_ai.phase_ms",
            unit: "ms",
            description: "mob-ai time per phase per invocation, tagged by phase");

        InputQueueDepth = _meter.CreateHistogram<long>(
            "tapestry.input_queue.depth",
            description: "Queue depth sampled each tick");

        SessionDuration = _meter.CreateHistogram<double>(
            "tapestry.session.duration_s",
            unit: "s",
            description: "Total session duration on disconnect");

        FloodCommandsDropped = _meter.CreateCounter<long>(
            "tapestry_flood_commands_dropped",
            description: "Commands rejected by token bucket");

        FloodDisconnects = _meter.CreateCounter<long>(
            "tapestry_flood_disconnects",
            description: "Players disconnected for command flooding");

        BadInputTotal = _meter.CreateCounter<long>(
            "tapestry_bad_input_total",
            description: "Unrecognized command frequency");

        LinkDeadActive = _meter.CreateUpDownCounter<long>(
            "tapestry_linkdead_active",
            description: "Currently linkdead sessions");

        LinkDeadReconnected = _meter.CreateCounter<long>(
            "tapestry_linkdead_reconnected",
            description: "Successful reconnections after link-dead");

        LinkDeadExpired = _meter.CreateCounter<long>(
            "tapestry_linkdead_expired",
            description: "Linkdead sessions that timed out and were despawned");
    }

    /// <summary>
    /// Registers pull-based content-census gauges. Callbacks run only on metric
    /// collection (scrape), independent of the game loop, so they survive a meltdown.
    /// Call once at startup. Each gauge samples independently; sampling is one cheap
    /// pass over the world and collection is infrequent. If the sample is unavailable
    /// (a concurrent-mutation race returned null) the gauge emits nothing that scrape
    /// — a gap in the series, not a misleading zero.
    /// </summary>
    public void RegisterWorldCensus(Func<WorldCensus?> sampler)
    {
        _meter.CreateObservableGauge(
            "tapestry.world.entities",
            () => sampler() is { } c
                ? c.EntitiesByType.Select(kv =>
                    new Measurement<int>(kv.Value, new KeyValuePair<string, object?>("type", kv.Key)))
                : Array.Empty<Measurement<int>>(),
            description: "Live entity count by type");

        _meter.CreateObservableGauge(
            "tapestry.world.tag_index_tags",
            () => sampler() is { } c
                ? new[] { new Measurement<int>(c.TagCount) }
                : Array.Empty<Measurement<int>>(),
            description: "Distinct tag keys in the world tag index");

        _meter.CreateObservableGauge(
            "tapestry.world.tag_index_entries",
            () => sampler() is { } c
                ? new[] { new Measurement<long>((long)c.TagMemberships) }
                : Array.Empty<Measurement<long>>(),
            description: "Total entity-tag memberships across all tag sets");

        _meter.CreateObservableGauge(
            "tapestry.world.properties_total",
            () => sampler() is { } c
                ? new[] { new Measurement<long>(c.PropertiesTotal) }
                : Array.Empty<Measurement<long>>(),
            description: "Sum of property-bag entries across all entities");

        _meter.CreateObservableGauge(
            "tapestry.world.max_entity_properties",
            () => sampler() is { } c
                ? new[] { new Measurement<int>(c.MaxEntityProperties) }
                : Array.Empty<Measurement<int>>(),
            description: "Largest single entity property-bag size");
    }
}
