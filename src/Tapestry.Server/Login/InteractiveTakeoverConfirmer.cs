using Tapestry.Engine;
using Tapestry.Engine.Login;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Server.Login;

/// <summary>
/// Prompts the player over the connection to confirm taking over a live
/// session. Reproduces the LoginPhase.SessionTakeover transition (phase +
/// PhaseCts reset + timeout + GMCP phase) that LoginFlow.SetPhase performed,
/// then reads one line. Used by both telnet and web pre-auth.
/// </summary>
/// <remarks>
/// The <c>ct</c> passed to <see cref="ConfirmAsync"/> MUST be an external
/// lifetime/abort token (e.g. the web <c>context.RequestAborted</c>) and must
/// NOT be derived from <c>_context.PhaseCts</c>: <see cref="ConfirmAsync"/>
/// cancels and replaces <c>_context.PhaseCts</c> as its first action, so a
/// phase-derived <c>ct</c> would make the takeover read self-cancel before the
/// player can answer. Telnet callers that have no external abort token should
/// pass <see cref="System.Threading.CancellationToken.None"/>.
/// </remarks>
public class InteractiveTakeoverConfirmer : ITakeoverConfirmer
{
    private const string Prompt = "That character is already connected. Reconnect? (y/n)";
    private const string GmcpPrompt = "Character already connected. Reconnect?";

    private readonly AsyncConnectionAdapter _adapter;
    private readonly LoginContext _context;
    private readonly LoginHandler? _loginHandler;
    private readonly int _phaseTimeoutSeconds;

    public InteractiveTakeoverConfirmer(
        AsyncConnectionAdapter adapter,
        LoginContext context,
        LoginHandler? loginHandler,
        int phaseTimeoutSeconds)
    {
        _adapter = adapter;
        _context = context;
        _loginHandler = loginHandler;
        _phaseTimeoutSeconds = phaseTimeoutSeconds;
    }

    public async Task<bool> ConfirmAsync(CancellationToken ct)
    {
        // Reproduce LoginFlow.SetPhase(LoginPhase.SessionTakeover).
        _context.PhaseCts.Cancel();
        _context.PhaseCts = new CancellationTokenSource();
        _context.Phase = LoginPhase.SessionTakeover;
        if (_phaseTimeoutSeconds > 0)
        {
            _context.PhaseCts.CancelAfter(TimeSpan.FromSeconds(_phaseTimeoutSeconds));
        }
        _loginHandler?.SendLoginPhase(_context.ConnectionId, "sessiontakeover");

        _adapter.SendLine(Prompt);
        _loginHandler?.SendLoginPrompt(_context.ConnectionId, GmcpPrompt);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            ct, _context.PhaseCts.Token);
        try
        {
            var confirm = (await _adapter.ReadLineAsync(linked.Token))
                .Trim().ToLowerInvariant();
            return confirm is "y" or "yes";
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
