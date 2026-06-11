using System.Collections.Concurrent;
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
        npc.AddTag("hostile");
        world.SwapTagBuffers();

        var metrics = new TapestryMetrics();
        metrics.RegisterWorldCensus(world.SampleCensus);

        // ConcurrentBag, not List: the listener filters by meter NAME ("Tapestry"),
        // which every TapestryMetrics instance shares, so parallel test classes
        // emitting on their own "Tapestry" meters fire this listener's callbacks too.
        // Those concurrent Add()s used to corrupt a List mid-assertion ("Collection
        // was modified"). A bag's Add is thread-safe and its enumeration is a
        // moment-in-time snapshot, so Contain is race-free.
        var observed = new ConcurrentBag<(string instrument, int value, string? type)>();
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
        observed.Should().Contain(o => o.instrument == "tapestry.world.tag_index_tags" && o.value == 1);
        observed.Should().Contain(o => o.instrument == "tapestry.world.tag_index_entries" && o.value == 1);
    }

    [Fact]
    public void RegisterWorldCensus_NullSample_DoesNotThrow()
    {
        var metrics = new TapestryMetrics();
        metrics.RegisterWorldCensus(() => null);

        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Meter.Name == TapestryMetrics.MeterName) { l.EnableMeasurementEvents(inst); }
        };
        listener.SetMeasurementEventCallback<int>((inst, value, tags, state) => { });
        listener.SetMeasurementEventCallback<long>((inst, value, tags, state) => { });
        listener.Start();

        var act = () => listener.RecordObservableInstruments();

        act.Should().NotThrow();
    }
}
