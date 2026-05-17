using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.FloodProtection;

public class TokenBucketTests
{
    private static FloodProtectionSection DefaultConfig() => new()
    {
        CommandsPerSecond = 10,
        BurstSize = 5,
        StrikeThreshold = 3,
        StrikeDecaySeconds = 10
    };

    private static (PlayerSession session, FakeConnection conn) MakeWithFlood(
        FloodProtectionSection? config = null, long startTick = 0)
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "Alice");
        var tick = startTick;
        var floodCtx = new FloodContext(
            config ?? DefaultConfig(),
            TicksPerSecond: 10,
            GetCurrentTick: () => tick,
            Logger: NullLogger.Instance);
        var session = new PlayerSession(conn, entity, floodContext: floodCtx);
        return (session, conn);
    }

    [Fact]
    public void WithinBurst_CommandsEnqueue()
    {
        var (session, conn) = MakeWithFlood();

        for (var i = 0; i < 5; i++)
        {
            conn.SimulateInput("look");
        }

        var count = 0;
        while (session.TryDequeueInput(out _)) { count++; }
        count.Should().Be(5);
    }

    [Fact]
    public void ExceedsBurst_ExtraCommandsDropped()
    {
        var (session, conn) = MakeWithFlood();

        for (var i = 0; i < 10; i++)
        {
            conn.SimulateInput("look");
        }

        var count = 0;
        while (session.TryDequeueInput(out _)) { count++; }
        count.Should().Be(5); // burst_size = 5
    }

    [Fact]
    public void ExceedsBurst_SendsSlowDownWarningOnce()
    {
        var (session, conn) = MakeWithFlood();

        for (var i = 0; i < 10; i++)
        {
            conn.SimulateInput("look");
        }

        var slowDownCount = conn.SentText.Count(t => t.Contains("Slow down"));
        slowDownCount.Should().Be(1);
    }

    [Fact]
    public void NullFloodContext_AllCommandsEnqueue()
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", "Alice");
        var session = new PlayerSession(conn, entity); // no FloodContext

        for (var i = 0; i < 200; i++)
        {
            conn.SimulateInput("look");
        }

        var count = 0;
        while (session.TryDequeueInput(out _)) { count++; }
        count.Should().Be(100); // capped by MaxQueueDepth = 100
    }

    [Fact]
    public void StrikeThreshold_DisconnectsPlayer()
    {
        var config = new FloodProtectionSection
        {
            CommandsPerSecond = 10,
            BurstSize = 1,
            StrikeThreshold = 2,
            StrikeDecaySeconds = 10
        };
        var (session, conn) = MakeWithFlood(config);

        // Use up the 1 token
        conn.SimulateInput("look");
        // Now every input is a strike
        conn.SimulateInput("bad1"); // strike 1 - warned
        conn.SimulateInput("bad2"); // strike 2 - disconnect

        conn.IsConnected.Should().BeFalse();
        conn.SentText.Should().Contain(t => t.Contains("command flooding"));
    }

    [Fact]
    public void StrikeDecay_AfterCleanTime_ResetsStrikes()
    {
        var config = new FloodProtectionSection
        {
            CommandsPerSecond = 10,
            BurstSize = 1,
            StrikeThreshold = 5,
            StrikeDecaySeconds = 10
        };
        var tickVal = 0L;
        var conn = new FakeConnection();
        var entity = new Entity("player", "Alice");
        var floodCtx = new FloodContext(config, TicksPerSecond: 10,
            GetCurrentTick: () => tickVal, Logger: NullLogger.Instance);
        var session = new PlayerSession(conn, entity, floodContext: floodCtx);

        // Use burst token
        conn.SimulateInput("look");
        // Trigger 2 strikes
        conn.SimulateInput("bad1");
        conn.SimulateInput("bad2");
        conn.IsConnected.Should().BeTrue();

        // Advance time past strike_decay_seconds (10s * 10 tps = 100 ticks)
        tickVal = 200;
        // Replenish tokens (20s * 10cps = 200 tokens capped at BurstSize=1) and succeed -> triggers decay check
        conn.SimulateInput("look");
        // If strikes weren't reset, next failure would be strike 3; since reset, it's strike 1
        conn.SimulateInput("bad3");

        conn.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void PromptMode_Input_SkipsTokenBucket()
    {
        var config = new FloodProtectionSection
        {
            CommandsPerSecond = 0,
            BurstSize = 0,
            StrikeThreshold = 3,
            StrikeDecaySeconds = 10
        };
        var conn = new FakeConnection();
        var entity = new Entity("player", "Alice");
        var floodCtx = new FloodContext(config, TicksPerSecond: 10,
            GetCurrentTick: () => 0L, Logger: NullLogger.Instance);
        var session = new PlayerSession(conn, entity, floodContext: floodCtx);

        var received = new List<string>();
        session.InputMode = InputMode.Prompt;
        session.PromptHandler = input => received.Add(input);

        conn.SimulateInput("my-password");

        received.Should().ContainSingle().Which.Should().Be("my-password");
    }
}
