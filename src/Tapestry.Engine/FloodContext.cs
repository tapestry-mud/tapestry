using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Tapestry.Data;

namespace Tapestry.Engine;

public sealed record FloodContext(
    FloodProtectionSection Config,
    int TicksPerSecond,
    Func<long> GetCurrentTick,
    ILogger? Logger = null,
    Counter<long>? DroppedCounter = null,
    Counter<long>? DisconnectCounter = null);
