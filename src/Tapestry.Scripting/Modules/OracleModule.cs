using Tapestry.Engine;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class OracleModule : IJintApiModule
{
    private readonly OracleTableRegistry _oracle;

    public string Namespace => "oracle";

    public OracleModule(OracleTableRegistry oracle)
    {
        _oracle = oracle;
    }

    public object Build(JintEngine engine)
    {
        return new
        {
            table = new Func<string, object?>(id =>
            {
                var t = _oracle.Get(id);
                if (t == null) { return null; }
                return new
                {
                    id = t.Id,
                    kind = t.Kind,
                    entries = t.Entries.Select(e => (object)new
                    {
                        w = e.W,
                        id = e.Id,
                        name = e.Name,
                        desc = e.Desc,
                        balance_ref = e.BalanceRef,
                        rarity = e.Rarity,
                        extra = e.Extra,
                    }).ToArray(),
                };
            }),
        };
    }
}
