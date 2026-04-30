using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Networking;
using Tapestry.Scripting;
using Tapestry.Shared;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Server;
using Tapestry.Server.Login;
using Tapestry.Server.Persistence;
using Tapestry.Scripting.Modules;
using Microsoft.AspNetCore.Builder;

// Load config early for Serilog and telemetry setup
var configPath = args.Length > 0 ? args[0] : "server.yaml";
if (!File.Exists(configPath))
{
    configPath = Path.Combine(AppContext.BaseDirectory, configPath);
}
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Config file not found: {configPath}");
    return;
}

var config = ServerConfig.Load(configPath);

// Configure Serilog
var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

if (config.Telemetry.Enabled)
{
    logConfig
        .Enrich.WithProperty("Service", config.Telemetry.ServiceName)
        .Enrich.WithMachineName()
        .WriteTo.OpenTelemetry(opts =>
        {
            var baseUri = new Uri(config.Telemetry.Endpoint);
            opts.Endpoint = $"http://{baseUri.Host}:4318/v1/logs";
            opts.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
            opts.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = config.Telemetry.ServiceName
            };
        });
}

Log.Logger = logConfig.CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{config.Server.WebsocketPort}");
builder.Host.ConfigureHostOptions(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(5));
builder.Services.AddSerilog();

// Register config
builder.Services.AddSingleton(config);

// Register engine, scripting services
builder.Services.AddTapestryEngine();
builder.Services.AddTapestryScripting();

// Persistence
builder.Services.AddSingleton<IPlayerStore, FilePlayerStore>();
builder.Services.AddSingleton<PlayerPersistenceService>();
builder.Services.AddSingleton<IFlowPersistence, FlowPersistenceAdapter>();
builder.Services.AddSingleton<LoginGateRegistry>();

// TelnetServer needs port from config
builder.Services.AddSingleton(sp =>
{
    var sessions = sp.GetRequiredService<SessionManager>();
    var startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    return new TelnetServer(
        config.Server.TelnetPort,
        config.Networking.NegotiationTimeoutMs,
        sp.GetRequiredService<ILogger<TelnetServer>>(),
        config.Mssp,
        getMsspDynamic: () => new MsspDynamicValues
        {
            Players = sessions.Count,
            UptimeEpoch = startTime
        });
});

builder.Services.AddSingleton<GmcpService>();
builder.Services.AddSingleton<GmcpModuleAdapter>();
builder.Services.AddSingleton<IGmcpModuleAdapter>(sp => sp.GetRequiredService<GmcpModuleAdapter>());
builder.Services.AddSingleton<ConnectionHandler>();

// Bootstrapper and hosted services
builder.Services.AddSingleton<GameBootstrapper>();
builder.Services.AddHostedService<GameLoopService>();
builder.Services.AddHostedService<TelnetService>();

// Telemetry (conditional)
if (config.Telemetry.Enabled)
{
    builder.Services.AddSingleton(Sdk.CreateMeterProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(config.Telemetry.ServiceName))
        .AddMeter(TapestryMetrics.MeterName)
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(opts => { opts.Endpoint = new Uri(config.Telemetry.Endpoint); })
        .Build());

    builder.Services.AddSingleton(Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(config.Telemetry.ServiceName))
        .AddSource(TapestryTracing.SourceName)
        .AddOtlpExporter(opts => { opts.Endpoint = new Uri(config.Telemetry.Endpoint); })
        .Build());
}

var app = builder.Build();

// WebSocket endpoint for web client connections
app.UseWebSockets();
app.Run(async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connections only");
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    var handler = context.RequestServices.GetRequiredService<ConnectionHandler>();
    var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
    var wsLogger = loggerFactory.CreateLogger<WebSocketConnection>();

    var connection = new WebSocketConnection(ws, wsLogger);
    wsLogger.LogInformation("New WebSocket connection: {Id} from {Remote}",
        connection.Id, context.Connection.RemoteIpAddress);

    handler.HandleNewConnection(connection, connection.GmcpHandler);
    await connection.RunAsync(context.RequestAborted);
});

// Bootstrap: load packs, wire events, register tick handlers
var loginGates = app.Services.GetRequiredService<LoginGateRegistry>();
loginGates.Register(new ReservedNameGate());
app.Services.GetRequiredService<GameBootstrapper>().Configure();

Log.Information("Starting {Name}...", config.Server.Name);
Log.Information("{Name} is running. Telnet: {TelnetPort}, WebSocket: {WsPort}. Ctrl+C to stop.",
    config.Server.Name, config.Server.TelnetPort, config.Server.WebsocketPort);

await app.RunAsync();
await Log.CloseAndFlushAsync();
