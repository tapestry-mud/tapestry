using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tapestry.Engine.Registration;
using Tapestry.Shared;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class PacksModule : IJintApiModule
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PacksModule> _logger;

    public PacksModule(
        IServiceProvider services,
        ILogger<PacksModule> logger)
    {
        _services = services;
        _logger = logger;
    }

    public string Namespace => "packs";

    public object Build(JintEngine engine)
    {
        return new
        {
            list = new Func<object[]>(ListPacks),
            getAll = new Func<object[]>(ListPacks),
        };
    }

    private object[] ListPacks()
    {
        return _services.GetRequiredService<PackLoader>().LoadedPacks
            .OrderBy(p => p.LoadOrder)
            .Select(p => (object)new
            {
                name = PackLoader.PackNamespace(p.Name),
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
