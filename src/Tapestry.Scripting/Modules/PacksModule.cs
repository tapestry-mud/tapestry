using JintEngine = Jint.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Tapestry.Scripting.Modules;

public class PacksModule : IJintApiModule
{
    private readonly IServiceProvider _services;

    public PacksModule(IServiceProvider services)
    {
        _services = services;
    }

    public string Namespace => "packs";

    public object Build(JintEngine engine)
    {
        var listFunc = new Func<object[]>(ListPacks);
        return new
        {
            list = listFunc,
            getAll = listFunc
        };
    }

    private object[] ListPacks()
    {
        return _services.GetRequiredService<PackLoader>().LoadedPacks
            .OrderBy(p => p.LoadOrder)
            .Select(p => (object)new
            {
                name = p.Name,
                displayName = p.DisplayName,
                version = p.Version,
                description = p.Description,
                author = p.Author,
                copyright = p.Copyright,
                website = p.Website,
                license = p.License
            })
            .ToArray();
    }
}
