using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class NotificationsModule : IJintApiModule
{
    private readonly NotificationQueue _queue;

    public string Namespace => "notifications";

    public NotificationsModule(NotificationQueue queue)
    {
        _queue = queue;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            enqueue = new Action<string, string, int, string>((entityIdStr, type, priority, text) =>
            {
                if (Guid.TryParse(entityIdStr, out var entityId))
                {
                    _queue.Enqueue(entityId, new Notification(type, priority, text));
                }
            })
        };
    }
}
