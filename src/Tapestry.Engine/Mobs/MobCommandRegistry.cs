// src/Tapestry.Engine/Mobs/MobCommandRegistry.cs
using Microsoft.Extensions.Logging;
using Tapestry.Shared;

namespace Tapestry.Engine.Mobs;

public class MobCommandRegistry
{
    private readonly Dictionary<string, MobCommandRegistration> _commands = new();
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly ILogger<MobCommandRegistry> _logger;

    public MobCommandRegistry(World world, EventBus eventBus, ILogger<MobCommandRegistry> logger)
    {
        _world = world;
        _eventBus = eventBus;
        _logger = logger;
    }

    public void Register(string verb, MobCommandRegistration registration)
    {
        _commands[verb.ToLower()] = registration;
    }

    public void Dispatch(Guid entityId, string commandStr)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null || entity.LocationRoomId == null)
        {
            return;
        }

        var spaceIdx = commandStr.IndexOf(' ');
        var verb = (spaceIdx > 0 ? commandStr[..spaceIdx] : commandStr).ToLower();
        var text = spaceIdx > 0 ? commandStr[(spaceIdx + 1)..] : "";

        if (!_commands.TryGetValue(verb, out var reg))
        {
            _logger.LogDebug("Unknown mob command verb: {Verb}", verb);
            return;
        }

        var context = new MobContext
        {
            EntityId = entityId,
            Name = entity.Name,
            RoomId = entity.LocationRoomId
        };

        try
        {
            reg.Handler(context, text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mob command handler error: verb={Verb} entity={EntityId}", verb, entityId);
            return;
        }

        if (reg.GmcpChannel != null)
        {
            var gmcpText = reg.PrependSender ? entity.Name + " " + text : text;
            _eventBus.Publish(new GameEvent
            {
                Type = "communication.message",
                Data = new Dictionary<string, object?>
                {
                    ["channel"] = reg.GmcpChannel,
                    ["sender"] = entity.Name,
                    ["senderId"] = entityId.ToString(),
                    ["source"] = "mob",
                    ["text"] = gmcpText,
                    ["roomId"] = entity.LocationRoomId
                }
            });
        }
    }
}
