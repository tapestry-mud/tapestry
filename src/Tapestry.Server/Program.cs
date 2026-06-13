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
using Tapestry.Server.GameEntry;
using Tapestry.Server.Login;
using Tapestry.Server.Persistence;
using Tapestry.Server.Modules;
using Tapestry.Server.PreAuth;
using Tapestry.Scripting.Modules;
using Tapestry.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using System.Threading.RateLimiting;

// Load config early for Serilog and telemetry setup.
// --config <path> and --packs <dir> are parsed via the command-line config provider.
var launchConfig = new ConfigurationBuilder().AddCommandLine(args).Build();
var (configArg, packsOverride) = LaunchOptions.Resolve(launchConfig);

var configPath = configArg;
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
config.PacksDirectory = packsOverride;

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
builder.Services.AddSingleton<IAccountStore, FileAccountStore>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<PlayerPersistenceService>();
builder.Services.AddSingleton<IFlowPersistence, FlowPersistenceAdapter>();
builder.Services.AddSingleton<Tapestry.Engine.Quests.QuestPersistenceService>();
builder.Services.AddSingleton<Tapestry.Engine.Quests.IQuestPersistence>(
    sp => sp.GetRequiredService<Tapestry.Engine.Quests.QuestPersistenceService>());
builder.Services.AddSingleton<LoginGateRegistry>();
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<ServerConfig>();
    return new PreAuthTokenService(cfg.PreAuth.TokenExpirySeconds);
});
builder.Services.AddSingleton<AccountSessionService>();
builder.Services.AddSingleton(_ => new ConnectionLimiter(config.Server.MaxConnections));

// TelnetServer needs port from config
builder.Services.AddSingleton(sp =>
{
    var limiter = sp.GetRequiredService<ConnectionLimiter>();
    var sessions = sp.GetRequiredService<SessionManager>();
    var startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    var keepAlive = config.Networking.KeepAlive;
    var server = new TelnetServer(
        config.Server.TelnetPort,
        config.Networking.NegotiationTimeoutMs,
        sp.GetRequiredService<ILogger<TelnetServer>>(),
        keepAlive.Enabled,
        keepAlive.IdleSeconds,
        keepAlive.IntervalSeconds,
        keepAlive.RetryCount,
        keepAlive.UserTimeoutSeconds * 1000,
        config.Mssp,
        getMsspDynamic: () => new MsspDynamicValues
        {
            Players = sessions.Count,
            UptimeEpoch = startTime
        });

    server.ConnectionGate = () => limiter.TryAcquire();
    server.OnConnectionReleased = () => limiter.Release();

    return server;
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
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler, Tapestry.Server.Gmcp.Handlers.QuestHandler>();
builder.Services.AddSingleton<Tapestry.Server.Gmcp.Handlers.NotificationHandler>();
builder.Services.AddSingleton<Tapestry.Contracts.IGmcpPackageHandler>(
    sp => sp.GetRequiredService<Tapestry.Server.Gmcp.Handlers.NotificationHandler>());
builder.Services.AddSingleton<Tapestry.Server.Gmcp.Handlers.LoginHandler>();
// NOTE: LoginHandler is NOT registered as IGmcpPackageHandler to avoid circular DI:
// PostLoginOrchestrator -> IEnumerable<IGmcpPackageHandler> -> LoginHandler -> PostLoginOrchestrator
// LoginHandler.Configure() is called explicitly during bootstrap instead.

// Game modules -- order is boot order
builder.Services.AddSingleton<IGameModule, ConfigurationModule>();
builder.Services.AddSingleton<IGameModule, ContentLoadingModule>();
builder.Services.AddSingleton<QuestScriptCoverageVerifier>();
builder.Services.AddSingleton<IGameModule, QuestStartupModule>();
builder.Services.AddSingleton<IGameModule, CombatEventModule>();
builder.Services.AddSingleton<IGameModule, WorldEventModule>();
builder.Services.AddSingleton<IGameModule, TickHandlerModule>();
builder.Services.AddSingleton<IGameModule, PersistenceModule>();
// PlayerInitModule is registered as both concrete (GameLoopService calls LoadSeedPlayers
// post-seal) and IGameModule (bootstrap calls Configure) -- same singleton.
builder.Services.AddSingleton<PlayerInitModule>();
builder.Services.AddSingleton<IGameModule>(sp => sp.GetRequiredService<PlayerInitModule>());
builder.Services.AddSingleton<IGameModule, BadInputModule>();

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

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth-strict", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("auth-light", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

if (config.Networking.TrustedProxies.Count > 0)
{
    app.UseForwardedHeaders(
        ForwardedHeadersOptionsBuilder.Build(config.Networking.TrustedProxies));
}

// WebSocket endpoint for web client connections
app.UseWebSockets();
app.UseRateLimiter();

// --- Pre-Auth HTTP Endpoints ---

app.MapGet("/config", () =>
{
    return Results.Json(new { preAuth = new { enabled = config.PreAuth.Enabled } });
});

app.MapGet("/auth/check", (string? name, PlayerPersistenceService persistence) =>
{
    var (valid, error) = NameValidator.Validate(name);
    if (!valid)
    {
        return Results.Json(new { exists = false, nameValid = false, error });
    }

    var canonical = NameValidator.Canonicalize(name!);
    var exists = persistence.PlayerSaveExists(canonical);

    return Results.Json(new { exists, nameValid = true, error = (string?)null });
}).RequireRateLimiting("auth-light");

app.MapGet("/auth/check-email", (string? email, AccountService accountService) =>
{
    if (string.IsNullOrWhiteSpace(email))
    {
        return Results.Json(new { exists = false });
    }

    var (valid, _) = EmailValidator.Validate(email);
    if (!valid)
    {
        return Results.Json(new { exists = false });
    }

    var exists = accountService.ExistsByEmail(email);
    return Results.Json(new { exists });
}).RequireRateLimiting("auth-light");

// Credential endpoints exist only when pre-auth (account/character login) is enabled.
if (config.PreAuth.Enabled)
{
    app.MapPost("/auth/login", async (HttpContext httpContext, AccountService accountService,
        PreAuthTokenService tokenService, AccountSessionService accountSessionService) =>
    {
        var body = await httpContext.Request.ReadFromJsonAsync<PreAuthLoginRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Email))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "Email is required" });
        }

        if (string.IsNullOrEmpty(body.Password))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "Password is required" });
        }

        var (emailValid, emailError) = EmailValidator.Validate(body.Email);
        if (!emailValid)
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = emailError });
        }

        var account = await accountService.Authenticate(body.Email, body.Password);
        if (account == null)
        {
            httpContext.Response.StatusCode = 401;
            return Results.Json(new { error = "Invalid email or password" });
        }

        var sessionToken = accountSessionService.Issue(account.Id);

        return Results.Json(new
        {
            session_token = sessionToken,
            account_id = account.Id.ToString(),
            characters = account.Characters
        });
    }).RequireRateLimiting("auth-strict");

    app.MapPost("/auth/select", async (HttpContext httpContext, AccountService accountService,
        PlayerPersistenceService persistence, SessionManager sessions, PreAuthTokenService tokenService,
        LoginGateRegistry loginGates, AccountSessionService accountSessionService) =>
    {
        var body = await httpContext.Request.ReadFromJsonAsync<PreAuthSelectRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.SessionToken))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "session_token is required" });
        }

        var accountSession = accountSessionService.Consume(body.SessionToken);
        if (accountSession == null)
        {
            httpContext.Response.StatusCode = 401;
            return Results.Json(new { error = "Invalid or expired session token" });
        }

        if (string.IsNullOrWhiteSpace(body.NewCharacter))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "new_character is required" });
        }

        var charName = body.NewCharacter.Trim();
        var (nameValid, nameError) = NameValidator.Validate(charName);
        if (!nameValid)
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = nameError });
        }

        var canonical = NameValidator.Canonicalize(charName);
        if (persistence.PlayerSaveExists(canonical))
        {
            httpContext.Response.StatusCode = 409;
            return Results.Json(new { error = "Character name already exists" });
        }

        var gateResult = loginGates.RunAll(canonical, null!);
        if (!gateResult.Allowed)
        {
            httpContext.Response.StatusCode = 403;
            return Results.Json(new { error = gateResult.Message ?? "Name not allowed" });
        }

        await accountService.AddCharacterToAccount(accountSession.AccountId, canonical);
        var token = tokenService.Issue(canonical, accountSession.AccountId, PreAuthIntent.Create);
        return Results.Json(new { token });
    }).RequireRateLimiting("auth-strict");

    app.MapPost("/auth/login-by-character", async (HttpContext httpContext,
        PlayerPersistenceService persistence, AccountService accountService,
        SessionManager sessions, PreAuthTokenService tokenService) =>
    {
        var body = await httpContext.Request.ReadFromJsonAsync<PreAuthLoginByCharacterRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Character))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "character is required" });
        }

        if (string.IsNullOrEmpty(body.Password))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "password is required" });
        }

        var canonical = NameValidator.Canonicalize(body.Character);
        if (!persistence.PlayerSaveExists(canonical))
        {
            httpContext.Response.StatusCode = 404;
            return Results.Json(new { error = "Character not found" });
        }

        var playerData = await persistence.LoadPlayer(canonical);
        if (playerData == null || playerData.AccountId == Guid.Empty)
        {
            httpContext.Response.StatusCode = 500;
            return Results.Json(new { error = "Character data unavailable" });
        }

        var account = await accountService.AuthenticateById(playerData.AccountId, body.Password);
        if (account == null)
        {
            httpContext.Response.StatusCode = 401;
            return Results.Json(new { error = "Invalid password" });
        }

        if (sessions.IsAccountAtCharacterLimit(
                account.Id, config.Accounts.MaxConcurrentCharacters, playerData.Entity.Id))
        {
            httpContext.Response.StatusCode = 409;
            return Results.Json(new { error = "Concurrent character limit reached" });
        }

        var token = tokenService.Issue(canonical, account.Id, PreAuthIntent.Login);
        return Results.Json(new { token });
    }).RequireRateLimiting("auth-strict");

    app.MapPost("/auth/register", async (HttpContext httpContext,
        AccountService accountService, PlayerPersistenceService persistence,
        LoginGateRegistry loginGates, PreAuthTokenService tokenService) =>
    {
        var body = await httpContext.Request.ReadFromJsonAsync<PreAuthRegisterRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Email))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "email is required" });
        }

        if (string.IsNullOrEmpty(body.Password))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "password is required" });
        }

        if (string.IsNullOrWhiteSpace(body.Character))
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = "character is required" });
        }

        var (pwOk, pwError) = PasswordValidator.Validate(body.Password, config.Persistence.PasswordMinLength);
        if (!pwOk)
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = pwError });
        }

        var (emailValid, emailError) = EmailValidator.Validate(body.Email);
        if (!emailValid)
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = emailError });
        }

        var (nameValid, nameError) = NameValidator.Validate(body.Character);
        if (!nameValid)
        {
            httpContext.Response.StatusCode = 400;
            return Results.Json(new { error = nameError });
        }

        var canonical = NameValidator.Canonicalize(body.Character);
        if (persistence.PlayerSaveExists(canonical))
        {
            httpContext.Response.StatusCode = 409;
            return Results.Json(new { error = "Character name already exists" });
        }

        if (accountService.ExistsByEmail(body.Email))
        {
            httpContext.Response.StatusCode = 409;
            return Results.Json(new { error = "An account with this email already exists" });
        }

        var gateResult = loginGates.RunAll(canonical, null!);
        if (!gateResult.Allowed)
        {
            httpContext.Response.StatusCode = 403;
            return Results.Json(new { error = gateResult.Message ?? "Name not allowed" });
        }

        var account = await accountService.CreateAccount(body.Email, body.Password);
        await accountService.AddCharacterToAccount(account.Id, canonical);
        var token = tokenService.Issue(canonical, account.Id, PreAuthIntent.Create);
        return Results.Json(new { token });
    }).RequireRateLimiting("auth-strict");
}

// --- WebSocket fallback (runs only when no HTTP route matched) ---

app.MapFallback(async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connections only");
        return;
    }

    var limiter = context.RequestServices.GetRequiredService<ConnectionLimiter>();
    if (!limiter.TryAcquire())
    {
        context.Response.StatusCode = 503;
        await context.Response.WriteAsync("Server full. Try again later.");
        return;
    }

    try
    {
        var wsKeepAlive = config.Networking.KeepAlive;
        using var ws = wsKeepAlive.Enabled
            ? await context.WebSockets.AcceptWebSocketAsync(
                WebSocketKeepAlive.BuildAcceptContext(wsKeepAlive.IntervalSeconds, wsKeepAlive.RetryCount))
            : await context.WebSockets.AcceptWebSocketAsync();
        var handler = context.RequestServices.GetRequiredService<ConnectionHandler>();
        var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        var wsLogger = loggerFactory.CreateLogger<WebSocketConnection>();

        // Tokenless anonymous watch mode (Slice B): a read-only spectator stream, not a player. It
        // skips the login state machine entirely, never becomes a player, and speaks only the tiny
        // watch control protocol. Off unless server.yaml enables it (watch.enabled) — a server that
        // does not want watching leaves the transport off and the tee primitive stays dormant.
        var watchMode = context.Request.Query["mode"].FirstOrDefault();
        if (string.Equals(watchMode, "watch", StringComparison.OrdinalIgnoreCase))
        {
            if (!config.Watch.Enabled)
            {
                wsLogger.LogInformation("Rejected watch connection: watch mode disabled");
                return; // `using var ws` closes the socket
            }

            var watchLogger = loggerFactory.CreateLogger<WatchSinkConnection>();
            var watchConn = new WatchSinkConnection(ws, watchLogger)
            {
                RemoteAddress = context.Connection.RemoteIpAddress?.ToString()
            };

            var watchRegistry = context.RequestServices.GetRequiredService<Tapestry.Engine.Watch.WatchRegistry>();
            var watchRosterSource = context.RequestServices.GetRequiredService<Tapestry.Engine.Watch.IWatchRosterSource>();
            var watchHub = context.RequestServices.GetRequiredService<Tapestry.Engine.Watch.WatchSessionHub>();

            var watchSession = new Tapestry.Engine.Watch.WatchSession(watchConn, watchRegistry, watchRosterSource);
            watchHub.Register(watchSession);
            watchConn.OnDisconnected += () => watchHub.Unregister(watchConn.Id);

            wsLogger.LogInformation("New watch (spectator) connection: {Id} from {Remote}",
                watchConn.Id, context.Connection.RemoteIpAddress);

            watchSession.PushRoster();
            watchConn.SendStatus("Connected. Pick a player to watch.");

            await watchConn.RunAsync(context.RequestAborted);
            return;
        }

        var connection = new WebSocketConnection(ws, wsLogger)
        {
            RemoteAddress = context.Connection.RemoteIpAddress?.ToString()
        };
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

                // Color renders first (outermost); the word-wrapper runs on the rendered
                // output (innermost) where ANSI escapes are zero-width.
                var colorRenderer = context.RequestServices.GetRequiredService<ColorRenderer>();
                var outputWrapper = context.RequestServices.GetRequiredService<Tapestry.Engine.Text.OutputWrapper>();
                var outputWidthService = context.RequestServices.GetRequiredService<Tapestry.Engine.Text.OutputWidthService>();
                var watchRegistry = context.RequestServices.GetRequiredService<Tapestry.Engine.Watch.WatchRegistry>();
                var colorConn = OutputChainFactory.Build(
                    connection, colorRenderer, outputWrapper, outputWidthService, sessionMgr, watchRegistry);

                // Create and register LoginContext
                var loginContext = new LoginContext(connection.Id, colorConn);
                sessionMgr.RegisterPreLogin(loginContext);

                // Ensure the pre-login registration is cleaned up on every disconnect
                // path. The session-aware Login branch resolves on a background task;
                // its Declined / OverLimit / error / client-drop outcomes only
                // disconnect (success paths remove it via the spawner), so without
                // this hook those outcomes would leak the _preLogin entry (and inflate
                // ConnectionCount). RemovePreLogin is idempotent, so this is a no-op on
                // the success paths that already removed it.
                connection.OnDisconnected += () => sessionMgr.RemovePreLogin(connection.Id);

                var wizlock = context.RequestServices.GetRequiredService<WizlockState>();

                if (preAuthToken.Intent == PreAuthIntent.Login)
                {
                    var data = await persistence.LoadPlayer(preAuthToken.Name);
                    if (data != null && wizlock.Locked && !data.Entity.HasRole("admin"))
                    {
                        // Wizlock (ROM parity): refuse non-admin web logins at token
                        // redemption -- the one choke point every web login crosses.
                        colorConn.SendLine(WizlockState.Message);
                        colorConn.Disconnect("wizlock");
                    }
                    else if (data == null)
                    {
                        // Save no longer exists -> existing fallback (resolver not reached).
                        handler.HandleNewConnection(connection, connection.GmcpHandler);
                    }
                    else
                    {
                        // Resolve game entry exactly as telnet does. The resolver may
                        // await a takeover confirmation, which needs the WS input pump
                        // (RunAsync, below) running concurrently -- so run it on a
                        // background task, mirroring ConnectionHandler.HandleNewConnection.
                        var loginHandlerGmcp = context.RequestServices
                            .GetService<Tapestry.Server.Gmcp.Handlers.LoginHandler>();
                        var takeoverTimeout = config.Idle.PhaseTimeouts.SessionTakeover > 0
                            ? config.Idle.PhaseTimeouts.SessionTakeover
                            : config.Idle.PreLoginTimeoutSeconds;
                        var adapter = new AsyncConnectionAdapter(colorConn);
                        var confirmer = new InteractiveTakeoverConfirmer(
                            adapter, loginContext, loginHandlerGmcp, takeoverTimeout);
                        var resolver = new GameEntryResolver(sessionMgr, spawner, config);

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var result = await resolver.ResolveAsync(
                                    preAuthToken.AccountId, preAuthToken.Name, data,
                                    colorConn, loginContext, confirmer, context.RequestAborted);

                                switch (result)
                                {
                                    case GameEntryResult.OverLimit:
                                        colorConn.SendLine("You already have a character online. Disconnect first.");
                                        colorConn.Disconnect("concurrent character limit");
                                        break;
                                    case GameEntryResult.Declined:
                                        colorConn.Disconnect("takeover declined");
                                        break;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // Connection dropped mid-resolution; teardown is handled elsewhere.
                            }
                            catch (Exception ex)
                            {
                                wsLogger.LogError(ex, "Game entry error for {Id}", connection.Id);
                                if (connection.IsConnected)
                                {
                                    connection.Disconnect("internal error");
                                }
                            }
                            finally
                            {
                                adapter.Dispose();
                            }
                        });
                    }
                }
                else if (wizlock.Locked)
                {
                    // Create-intent tokens pass the WizlockGate at issue time, but the
                    // flag may have been toggled on between issue and redemption -- a
                    // brand-new character can never be an admin, so refuse here too.
                    colorConn.SendLine(WizlockState.Message);
                    colorConn.Disconnect("wizlock");
                }
                else
                {
                    spawner.CompleteNewCharacter(
                        preAuthToken.Name,
                        preAuthToken.AccountId,
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
    }
    finally
    {
        limiter.Release();
    }
});

// Bootstrap: load packs, wire events, register tick handlers
var loginGates = app.Services.GetRequiredService<LoginGateRegistry>();
loginGates.Register(new ReservedNameGate());
loginGates.Register(new WizlockGate(app.Services.GetRequiredService<WizlockState>()));
app.Services.GetRequiredService<GameBootstrapper>().Configure();

// Force-resolve event-subscriber singletons so their constructors wire up EventBus subscriptions
app.Services.GetRequiredService<Tapestry.Engine.Classes.ClassPathProcessor>();

// Configure GMCP handlers (event subscriptions, flush callbacks, etc.)
foreach (var handler in app.Services.GetRequiredService<IEnumerable<IGmcpPackageHandler>>())
{
    handler.Configure();
}
app.Services.GetRequiredService<Tapestry.Server.Gmcp.Handlers.LoginHandler>().Configure();

Log.Information("Starting {Name}...", config.Server.Name);
Log.Information("{Name} is running. Telnet: {TelnetPort}, WebSocket: {WsPort}. Ctrl+C to stop.",
    config.Server.Name, config.Server.TelnetPort, config.Server.WebsocketPort);

await app.RunAsync();
await Log.CloseAndFlushAsync();
