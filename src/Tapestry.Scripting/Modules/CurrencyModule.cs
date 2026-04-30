using Tapestry.Engine;
using Tapestry.Engine.Economy;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class CurrencyModule : IJintApiModule
{
    private readonly World _world;
    private readonly CurrencyService _currency;

    public string Namespace => "currency";

    public CurrencyModule(World world, CurrencyService currency)
    {
        _world = world;
        _currency = currency;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            getGold = new Func<string, int>((entityIdStr) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return 0; }
                var entity = _world.GetEntity(id);
                return entity?.GetProperty<int>(CurrencyProperties.Gold) ?? 0;
            }),

            addGold = new Func<string, int, string, int>((entityIdStr, amount, reason) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return 0; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return 0; }
                return _currency.AddGold(entity, amount, reason);
            }),

            setGold = new Func<string, int, string, int>((entityIdStr, amount, reason) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return 0; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return 0; }
                return _currency.SetGold(entity, amount, reason);
            })
        };
    }
}
