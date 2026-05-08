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
            var session = _sessions.GetByEntityId(ctx.PlayerEntityId);
            if (session != null)
            {
                _ = _persistence.SavePlayer(session);
                _sessions.SendToPlayer(ctx.PlayerEntityId, "Character saved.\r\n");
            }
        }, priority: 100, packName: "core",
           description: "Save your character to disk.",
           category: "system");

        _commandRegistry.Register("resetpassword", (ctx) =>
        {
            var session = _sessions.GetByEntityId(ctx.PlayerEntityId);
            if (session == null)
            {
                return;
            }

            if (ctx.Args.Length == 0)
            {
                var room = _world.GetRoom(session.PlayerEntity.LocationRoomId ?? "");
                if (room == null || !room.HasTag("safe"))
                {
                    _sessions.SendToPlayer(ctx.PlayerEntityId,
                        "You must be in a safe area to reset your password.\r\n");
                    return;
                }

                session.InputMode = InputMode.Prompt;
                _loginHandler.SendLoginPhase(session.Connection.Id, "password");
                session.Connection.SuppressEcho();
                _sessions.SendToPlayer(ctx.PlayerEntityId, "Enter current password:\r\n");

                void ExitPrompt(string message)
                {
                    session.Connection.RestoreEcho();
                    _loginHandler.SendLoginPhase(session.Connection.Id, "playing");
                    session.InputMode = InputMode.Normal;
                    session.PromptHandler = null;
                    _sessions.SendToPlayer(ctx.PlayerEntityId, message + "\r\n");
                }

                session.PromptHandler = (currentPw) =>
                {
                    currentPw = currentPw.Trim();
                    var existingHash = _persistence.GetPasswordHash(session.PlayerEntity.Id);
                    if (existingHash == null || !BCrypt.Net.BCrypt.Verify(currentPw, existingHash))
                    {
                        ExitPrompt("Incorrect password. Password reset cancelled.");
                        return;
                    }

                    session.Connection.SendLine("Enter new password:");
                    session.PromptHandler = (newPw) =>
                    {
                        newPw = newPw.Trim();
                        if (newPw.Length < _config.Persistence.PasswordMinLength)
                        {
                            ExitPrompt(
                                $"Password must be at least {_config.Persistence.PasswordMinLength} characters. " +
                                "Password reset cancelled.");
                            return;
                        }

                        session.Connection.SendLine("Confirm new password:");
                        session.PromptHandler = (confirmPw) =>
                        {
                            confirmPw = confirmPw.Trim();
                            if (confirmPw != newPw)
                            {
                                ExitPrompt("Passwords don't match. Password reset cancelled.");
                                return;
                            }

                            var newHash = BCrypt.Net.BCrypt.HashPassword(newPw);
                            _persistence.UpdatePasswordHash(session.PlayerEntity.Id, newHash);
                            _ = _persistence.SavePlayer(session);
                            ExitPrompt("Password updated.");
                        };
                    };
                };
                return;
            }

            if (ctx.Args.Length == 2)
            {
                if (!session.PlayerEntity.HasTag("admin"))
                {
                    _sessions.SendToPlayer(ctx.PlayerEntityId, "You don't have permission to do that.\r\n");
                    return;
                }

                var targetName = ctx.Args[0];
                var newPassword = ctx.Args[1];
                if (newPassword.Length < _config.Persistence.PasswordMinLength)
                {
                    _sessions.SendToPlayer(ctx.PlayerEntityId,
                        $"Password must be at least {_config.Persistence.PasswordMinLength} characters.\r\n");
                    return;
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);

                var targetSession = _sessions.GetByPlayerName(targetName);
                if (targetSession != null)
                {
                    _persistence.UpdatePasswordHash(targetSession.PlayerEntity.Id, hash);
                    _ = _persistence.SavePlayer(targetSession);
                    _sessions.SendToPlayer(targetSession.PlayerEntity.Id,
                        "Your password has been reset by an administrator.\r\n");
                }
                else
                {
                    var data = _persistence.LoadPlayer(targetName).GetAwaiter().GetResult();
                    if (data == null)
                    {
                        _sessions.SendToPlayer(ctx.PlayerEntityId, "Player not found.\r\n");
                        return;
                    }
                    _ = _persistence.SaveNewPlayer(data.Entity, hash);
                }

                _sessions.SendToPlayer(ctx.PlayerEntityId,
                    $"Password reset for {targetName}.\r\n");
            }
        }, priority: 100, packName: "core",
           description: "Change your password. Admins can reset another player's password.",
           category: "system");
    }
}
