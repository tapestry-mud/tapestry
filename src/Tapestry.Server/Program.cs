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
using Tapestry.Engine.Color;
using Tapestry.Engine.Flow;
using Tapestry.Networking;
using Tapestry.Scripting;
using Tapestry.Shared;
using Tapestry.Engine.Login;
using Tapestry.Engine.Persistence;
using Tapestry.Server;
using Tapestry.Server.Login;
using Tapestry.Server.Persistence;
using Tapestry.Server.Modules;
using Tapestry.Server.PreAuth;
using Tapestry.Scripting.Modules;
using Tapestry.Contracts;
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
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<ServerConfig>();
    return new PreAuthTokenService(cfg.PreAuth.TokenExpirySeconds);
});

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

builder.Services.AddSingleton<GmcpModuleAdapter>();
builder.Services.AddSingleton<IGmcpModuleAdapter>(sp => sp.GetRequiredService<GmcpModuleAdapter>());
builder.Services.AddSingleton<ConnectionHandler>();
builder.Services.AddSingleton<PlayerSpawner>();

// GMCP infrastructure
builder.Services.AddSingleton<Tapestry.Server.Gmcp.GmcpConnectionManager>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpConnectionManager>(
    sp => sp.GetRequiredService<Tapestry.Server.Gmcp.GmcpConnectionManager>());
builder.Services.AddSingleton<Tapestry.Server.Gmcp.DirtyVitalsBatcher>();
builder.Services.AddSingleton<Tapestry.Contracts.IDirtyVitalsBatcher>(
    sp => sp.GetRequiredService<Tapestry.Server.Gmcp.DirtyVitalsBatcher>());
builder.Services.AddSingleton<Tapestry.Server.Gmcp.PostLoginOrchestrator>();

// GMCP package handlers -- registered as both IGmcpPackageHandler (for DI collection) and concrete (for direct injection)
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.DisplayHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.CharStatusHandler>();
builder.Services.AddSingleton<Tapestry.Server.Gmcp.Handlers.CharVitalsHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler>(
    sp => sp.GetRequiredService<Tapestry.Server.Gmcp.Handlers.CharVitalsHandler>());
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.CharExperienceHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.CharCommandsHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.CharEffectsHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.CharItemsHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.RoomHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.WorldHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.CharCombatHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.CommHandler>();
builder.Services.AddSingleton<Tapestry.Server.Gmcp.Handlers.LoginHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler>(
    sp => sp.GetRequiredService<Tapestry.Server.Gmcp.Handlers.LoginHandler>());

// Game modules -- order is boot order
builder.Services.AddSingleton<IGameModule, ConfigurationModule>();
builder.Services.AddSingleton<IGameModule, ContentLoadingModule>();
builder.Services.AddSingleton<IGameModule, CombatEventModule>();
builder.Services.AddSingleton<IGameModule, WorldEventModule>();
builder.Services.AddSingleton<IGameModule, TickHandlerModule>();
builder.Services.AddSingleton<IGameModule, PersistenceModule>();
builder.Services.AddSingleton<IGameModule, PlayerInitModule>();

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
app.UseRouting();

// --- Pre-Auth HTTP Endpoints ---

app.MapGet("/config", () =>
{
    return Results.Json(new { preAuth = new { enabled = config.PreAuth.Enabled } });
});

app.MapGet("/auth/check", (string? name, PlayerPersistenceService persistence) =>
{
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.Json(new { exists = false, nameValid = false });
    }

    var nameValid = System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z]{2,20}$");
    var canonical = nameValid
        ? char.ToUpper(name[0]) + name[1..].ToLower()
        : name;
    var exists = nameValid && persistence.PlayerSaveExists(canonical);

    return Results.Json(new { exists, nameValid });
});

app.MapPost("/auth/login", async (HttpContext httpContext, PlayerPersistenceService persistence,
    SessionManager sessions, PreAuthTokenService tokenService, LoginGateRegistry loginGates) =>
{
    var body = await httpContext.Request.ReadFromJsonAsync<PreAuthLoginRequest>();
    if (body == null || string.IsNullOrWhiteSpace(body.Name))
    {
        httpContext.Response.StatusCode = 400;
        return Results.Json(new { error = "Name is required" });
    }

    var name = body.Name.Trim();
    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z]{2,20}$"))
    {
        httpContext.Response.StatusCode = 400;
        return Results.Json(new { error = "Names must be 2-20 letters only" });
    }

    var canonical = char.ToUpper(name[0]) + name[1..].ToLower();
    var exists = persistence.PlayerSaveExists(canonical);

    if (exists)
    {
        if (string.IsNullOrEmpty(body.Password))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "Password is required" });
        }

        var data = await persistence.LoadPlayer(canonical);
        if (data == null)
        {
            httpContext.Response.StatusCode = 500;
            return Results.Json(new { error = "Error loading character" });
        }

        if (!BCrypt.Net.BCrypt.Verify(body.Password, data.PasswordHash))
        {
            httpContext.Response.StatusCode = 401;
            return Results.Json(new { error = "Invalid name or password" });
        }

        var token = tokenService.Issue(canonical, PreAuthIntent.Login);
        return Results.Json(new { token });
    }
    else
    {
        if (string.IsNullOrEmpty(body.Password))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "Password is required" });
        }

        if (body.Password.Length < config.Persistence.PasswordMinLength)
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = $"Password must be at least {config.Persistence.PasswordMinLength} characters" });
        }

        if (body.ConfirmPassword != body.Password)
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "Passwords do not match" });
        }

        if (sessions.GetByPlayerName(canonical) != null)
        {
            httpContext.Response.StatusCode = 409;
            return Results.Json(new { error = "That name is currently in use" });
        }

        var gateResult = loginGates.RunAll(canonical, null!);
        if (!gateResult.Allowed)
        {
            httpContext.Response.StatusCode = 403;
            return Results.Json(new { error = gateResult.Message ?? "Name not allowed" });
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(body.Password);
        var token = tokenService.Issue(canonical, PreAuthIntent.Create, hash);
        return Results.Json(new { token });
    }
});

// --- WebSocket catch-all (must come after HTTP routes) ---

app.Map("/{**catch}", async context =>
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

    // Check for pre-auth token
    var tokenId = context.Request.Query["token"].FirstOrDefault();
    if (!string.IsNullOrEmpty(tokenId) && config.PreAuth.Enabled)
    {
        var tokenSvc = context.RequestServices.GetRequiredService<PreAuthTokenService>();
        var preAuthToken = tokenSvc.Consume(tokenId);

        if (preAuthToken != null)
        {
            var sessionMgr = context.RequestServices.GetRequiredService<SessionManager>();
            var persistence = context.RequestServices.GetRequiredService<PlayerPersistenceService>();
            var spawner = context.RequestServices.GetRequiredService<PlayerSpawner>();
            var connectionManager = context.RequestServices.GetRequiredService<IGmcpConnectionManager>();
            var flowEngine = context.RequestServices.GetService<FlowEngine>();

            // Register GMCP handler
            connectionManager.RegisterHandler(connection.Id, connection.GmcpHandler);
            connection.OnDisconnected += () => connectionManager.UnregisterHandler(connection.Id);

            // Wrap connection for color rendering
            var colorRenderer = context.RequestServices.GetRequiredService<ColorRenderer>();
            var colorConn = new ColorRenderingConnection(connection, colorRenderer);

            // Create and register LoginContext
            var loginContext = new LoginContext(connection.Id, colorConn);
            sessionMgr.RegisterPreLogin(loginContext);

            if (preAuthToken.Intent == PreAuthIntent.Login)
            {
                var data = await persistence.LoadPlayer(preAuthToken.Name);
                if (data != null)
                {
                    spawner.RestoreWorldObjects(data);
                    spawner.CompleteLogin(data.Entity, colorConn, loginContext);
                }
                else
                {
                    handler.HandleNewConnection(connection, connection.GmcpHandler);
                }
            }
            else
            {
                spawner.CompleteNewCharacter(
                    preAuthToken.Name,
                    preAuthToken.HashedPassword!,
                    colorConn,
                    loginContext,
                    flowEngine);
            }

            await connection.RunAsync(context.RequestAborted);
            return;
        }
    }

    // No token or invalid token -- normal LoginFlow
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
