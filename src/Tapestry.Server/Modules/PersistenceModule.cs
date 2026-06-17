using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Prompt;
using Tapestry.Engine.Registration;
using Tapestry.Server.Gmcp.Handlers;
using Tapestry.Server.Login;

namespace Tapestry.Server.Modules;

public class PersistenceModule : IGameModule
{
    private readonly CommandRegistry _commandRegistry;
    private readonly SessionManager _sessions;
    private readonly PlayerPersistenceService _persistence;
    private readonly AccountService _accountService;
    private readonly World _world;
    private readonly LoginHandler _loginHandler;
    private readonly ServerConfig _config;
    private readonly RegistrationGate? _gate;

    public string Name => "Persistence";

    public PersistenceModule(
        CommandRegistry commandRegistry,
        SessionManager sessions,
        PlayerPersistenceService persistence,
        AccountService accountService,
        World world,
        LoginHandler loginHandler,
        ServerConfig config,
        RegistrationGate? gate = null)
    {
        _commandRegistry = commandRegistry;
        _sessions = sessions;
        _persistence = persistence;
        _accountService = accountService;
        _world = world;
        _loginHandler = loginHandler;
        _config = config;
        _gate = gate;
    }

    public void Configure()
    {
        // Kernel-sanctioned write scope: this module's Configure runs AFTER
        // ContentLoadingModule (module registration order in Program.cs), i.e. after the
        // gate arms during pack loading. These are C# kernel commands that intentionally
        // coexist with same-keyword pack commands and win by priority -- routing them
        // through the RegistrationPolicy would turn that into a collision boot error.
        using var scope = _gate?.EnterCommitScope();
        _commandRegistry.Register("save", (ctx) =>
        {
            var session = _sessions.GetByEntityId(ctx.EntityId);
            if (session != null)
            {
                _ = _persistence.SavePlayer(session);
                _sessions.SendToPlayer(ctx.EntityId, "Character saved.\r\n");
            }
        }, priority: 100, packName: "core");

        _commandRegistry.Register("resetpassword", (ctx) =>
        {
            var session = _sessions.GetByEntityId(ctx.EntityId);
            if (session == null)
            {
                return;
            }

            if (ctx.RawArgs.Length == 0)
            {
                var room = _world.GetRoom(session.PlayerEntity.LocationRoomId ?? "");
                if (room == null || !room.HasTag("safe"))
                {
                    _sessions.SendToPlayer(ctx.EntityId,
                        "You must be in a safe area to reset your password.\r\n");
                    return;
                }

                var accountId = _accountService.GetAccountForEntity(session.PlayerEntity.Id);
                if (accountId == null)
                {
                    _sessions.SendToPlayer(ctx.EntityId, "No account linked to this character.\r\n");
                    return;
                }

                session.InputMode = InputMode.Prompt;
                _loginHandler.SendLoginPhase(session.Connection.Id, "password");
                session.Connection.SuppressEcho();
                _sessions.SendToPlayer(ctx.EntityId, "Enter current password:\r\n");

                var capturedAccountId = accountId.Value;
                var promptStep = 0;
                string? currentPassword = null;

                void ExitPrompt(string message)
                {
                    session.Connection.RestoreEcho();
                    _loginHandler.SendLoginPhase(session.Connection.Id, "playing");
                    session.InputMode = InputMode.Normal;
                    session.PromptHandler = null;
                    _sessions.SendToPlayer(ctx.EntityId, message + "\r\n");
                }

                session.PromptHandler = (input) =>
                {
                    input = input.Trim();
                    if (promptStep == 0)
                    {
                        currentPassword = input;
                        promptStep = 1;
                        _sessions.SendToPlayer(ctx.EntityId, "Enter new password:\r\n");
                    }
                    else if (promptStep == 1)
                    {
                        var (pwOk, pwError) = PasswordValidator.Validate(input, _config.Persistence.PasswordMinLength);
                        if (!pwOk)
                        {
                            ExitPrompt(pwError!);
                            return;
                        }

                        var changed = _accountService.ChangePassword(capturedAccountId, currentPassword!, input)
                            .GetAwaiter().GetResult();
                        if (!changed)
                        {
                            ExitPrompt("Current password is incorrect.");
                            return;
                        }

                        ExitPrompt("Password changed.");
                    }
                };
                return;
            }

            if (ctx.RawArgs.Length == 2)
            {
                if (!session.PlayerEntity.HasRole("admin"))
                {
                    _sessions.SendToPlayer(ctx.EntityId, "You don't have permission to do that.\r\n");
                    return;
                }

                var targetName = ctx.RawArgs[0];
                var newPassword = ctx.RawArgs[1];
                var (pwOk2, pwError2) = PasswordValidator.Validate(newPassword, _config.Persistence.PasswordMinLength);
                if (!pwOk2)
                {
                    _sessions.SendToPlayer(ctx.EntityId, pwError2 + "\r\n");
                    return;
                }

                Guid? targetAccountId = null;
                var targetSession = _sessions.GetByPlayerName(targetName);
                if (targetSession != null)
                {
                    targetAccountId = targetSession.AccountId;
                }
                else
                {
                    var data = _persistence.LoadPlayer(targetName).GetAwaiter().GetResult();
                    if (data == null)
                    {
                        _sessions.SendToPlayer(ctx.EntityId, "Player not found.\r\n");
                        return;
                    }
                    targetAccountId = data.AccountId;
                }

                if (targetAccountId == null || targetAccountId == Guid.Empty)
                {
                    _sessions.SendToPlayer(ctx.EntityId, "Player has no linked account.\r\n");
                    return;
                }

                var account = _accountService.LoadAccount(targetAccountId.Value).GetAwaiter().GetResult();
                if (account == null)
                {
                    _sessions.SendToPlayer(ctx.EntityId, "Account not found.\r\n");
                    return;
                }

                account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                _accountService.SaveAccount(account).GetAwaiter().GetResult();

                if (targetSession != null)
                {
                    _sessions.SendToPlayer(targetSession.PlayerEntity.Id,
                        "Your password has been reset by an administrator.\r\n");
                }

                _sessions.SendToPlayer(ctx.EntityId,
                    $"Password reset for {targetName}.\r\n");
            }
        }, priority: 100, packName: "core");
    }
}
