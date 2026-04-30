using Tapestry.Engine;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class ReturnAddressModule : IJintApiModule
{
    private readonly World _world;
    private readonly ReturnAddressService _returnAddress;

    public string Namespace => "returnaddress";

    public ReturnAddressModule(World world, ReturnAddressService returnAddress)
    {
        _world = world;
        _returnAddress = returnAddress;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            set = new Action<string, string>((entityIdStr, roomId) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return; }
                _returnAddress.SetReturn(entity, roomId);
            }),

            get = new Func<string, string?>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return null; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return null; }
                return _returnAddress.GetReturn(entity);
            }),

            clear = new Action<string>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return; }
                _returnAddress.ClearReturn(entity);
            }),

            has = new Func<string, bool>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return false; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return false; }
                return _returnAddress.HasReturn(entity);
            })
        };
    }
}
