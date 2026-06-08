using System.Diagnostics.Metrics;
using FluentAssertions;
using Tapestry.Engine;

namespace Tapestry.Engine.Tests;

public class WorldCensusMetricsTests
{
    [Fact]
    public void RegisterWorldCensus_ObservableGauges_ReportFromSampler()
    {
        var world = new World();
        var npc = new Entity("npc", "Goblin");
        npc.SetProperty("hp", 10);
        world.TrackEntity(npc);

        var metrics = new TapestryMetrics();
        metrics.RegisterWorldCensus(world.SampleCensus);

        var observed = new List<(string instrument, int value, string? type)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Meter.Name == TapestryMetrics.MeterName) { l.EnableMeasurementEvents(inst); }
        };
        listener.SetMeasurementEventCallback<int>((inst, value, tags, state) =>
        {
            string? type = null;
            foreach (var t in tags)
            {
                if (t.Key == "type") { type = t.Value?.ToString(); }
            }
            observed.Add((inst.Name, value, type));
        });
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) =>
            observed.Add((inst.Name, (int)value, null)));
        listener.Start();

        listener.RecordObservableInstruments();

        observed.Should().Contain(o => o.instrument == "tapestry.world.entities" && o.type == "npc" && o.value == 1);
        observed.Should().Contain(o => o.instrument == "tapestry.world.properties_total" && o.value == 1);
        observed.Should().Contain(o => o.instrument == "tapestry.world.max_entity_properties" && o.value == 1);
        observed.Should().Contain(o => o.instrument == "tapestry.world.tag_index_tags");
    }
}
