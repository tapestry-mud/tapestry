using Tapestry.Contracts;
using Tapestry.Engine;
using Tapestry.Shared;

namespace Tapestry.Server.Modules;

public class SwellEventModule : IGameModule
{
    private readonly EventBus _eventBus;
    private readonly SessionManager _sessions;

    public string Name => "SwellEvent";

    public SwellEventModule(EventBus eventBus, SessionManager sessions)
    {
        _eventBus = eventBus;
        _sessions = sessions;
    }

    public void Configure()
    {
        _eventBus.Subscribe("combat.swell.telegraph", Render);
        _eventBus.Subscribe("combat.swell.window", Render);
        _eventBus.Subscribe("combat.swell.resolve", Render);
    }

    private void Render(GameEvent evt)
    {
        if (evt.Data.TryGetValue("targetId", out var targetIdObj)
            && Guid.TryParse(targetIdObj?.ToString(), out var targetId)
            && evt.Data.TryGetValue("text", out var textObj)
            && textObj is string text)
        {
            _sessions.SendToPlayer(targetId, text + "\r\n");
        }
    }
}
