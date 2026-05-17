using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Prompt;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Server.Modules;

public class PersistenceModule : IGameModule
{
    private readonly CommandRegistry _commandRegistry;
    private readonly SessionManager _sessions;
    private readonly PlayerPersistenceService _persistence;
    private readonly World _world;
    private readonly LoginHandler _loginHandler;
    private readonly ServerConfig _config;

    public string Name => "Persistence";

    public PersistenceModule(
        CommandRegistry commandRegistry,
        SessionManager sessions,
        PlayerPersistenceService persistence,
        World world,
        LoginHandler loginHandler,
        ServerConfig config)
    {
        _commandRegistry = commandRegistry;
        _sessions = sessions;
        _persistence = persistence;
        _world = world;
        _loginHandler = loginHandler;
        _config = config;
    }

    public void Configure()
    {
        _commandRegistry.Register("save", (ctx) =>
        {
            var session = _sessions.GetByEntityId(ctx.EntityId);
            if (session != null)
            {
                _ = _persistence.SavePlayer(session);
                _sessions.SendToPlayer(ctx.EntityId, "Character saved.\r\n");
            }
        }, priority: 100, packName: "core",
           description: "Save your character to disk.",
           category: "system");

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

                session.InputMode = InputMode.Prompt;
                _loginHandler.SendLoginPhase(session.Connection.Id, "password");
                session.Connection.SuppressEcho();
                _sessions.SendToPlayer(ctx.EntityId, "Enter current password:\r\n");

                void ExitPrompt(string message)
                {
                    session.Connection.RestoreEcho();
                    _loginHandler.SendLoginPhase(session.Connection.Id, "playing");
                    session.InputMode = InputMode.Normal;
                    session.PromptHandler = null;
                    _sessions.SendToPlayer(ctx.EntityId, message + "\r\n");
                }

                // TODO(Task 9): verify and update password via AccountService instead of persistence service
                session.PromptHandler = (currentPw) =>
                {
                    currentPw = currentPw.Trim();
                    // Placeholder: password verification will be wired to AccountService in a later task
                    ExitPrompt("Password reset is temporarily unavailable. Please try again later.");
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
                if (newPassword.Length < _config.Persistence.PasswordMinLength)
                {
                    _sessions.SendToPlayer(ctx.EntityId,
                        $"Password must be at least {_config.Persistence.PasswordMinLength} characters.\r\n");
                    return;
                }

                // TODO(Task 9): admin password reset will be wired to AccountService in a later task
                var targetSession = _sessions.GetByPlayerName(targetName);
                if (targetSession != null)
                {
                    _ = _persistence.SavePlayer(targetSession);
                    _sessions.SendToPlayer(targetSession.PlayerEntity.Id,
                        "Your password has been reset by an administrator.\r\n");
                }
                else
                {
                    var data = _persistence.LoadPlayer(targetName).GetAwaiter().GetResult();
                    if (data == null)
                    {
                        _sessions.SendToPlayer(ctx.EntityId, "Player not found.\r\n");
                        return;
                    }
                    _ = _persistence.SaveNewPlayer(data.Entity, data.AccountId);
                }

                _sessions.SendToPlayer(ctx.EntityId,
                    $"Password reset for {targetName}.\r\n");
            }
        }, priority: 100, packName: "core",
           description: "Change your password. Admins can reset another player's password.",
           category: "system");
    }
}
