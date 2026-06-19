using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Registration;
using Tapestry.Scripting;
using Tapestry.Scripting.Interop;

namespace Tapestry.Scripting.Tests;

public class PackLoaderEsmDispatchTests
{
    [Fact]
    public void LoadContent_EsmFormat_RegistersViaModuleImport()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();

        // Temp pack: dist/scripts/init.js (ESM) + manifest flagged esm.
        var dir = Directory.CreateTempSubdirectory("tap-esm-pack-").FullName;
        Directory.CreateDirectory(Path.Combine(dir, "dist", "scripts"));
        File.WriteAllText(Path.Combine(dir, "dist", "scripts", "init.js"),
            "import { commands } from \"@tapestry/engine\";\n" +
            "commands.register({ name: 'esmping', priority: 0, handler: function(p,a){} });\n");
        File.WriteAllText(Path.Combine(dir, "pack.yaml"),
            "name: \"@tapestry/test-pack\"\nversion: \"0.0.1\"\ntype: module\n" +
            "content:\n  scripts: \"dist/scripts/**/*.js\"\n  scripts_format: esm\n");

        var packLoader = provider.GetRequiredService<PackLoader>();
        var manifest = packLoader.LoadDeclarations(dir);
        // Wire the loader's pack dir (ContentLoadingModule does this at boot in A6; do it directly here):
        provider.GetRequiredService<TapestryModuleLoader>()
            .Build(new Dictionary<string, string> { ["tapestry-test-pack"] = dir },
                   new HashSet<(string, string)>());
        packLoader.LoadContent(dir, manifest);

        provider.GetRequiredService<Tapestry.Engine.Registration.RegistrationPolicy>().Resolve();
        provider.GetRequiredService<CommandRegistry>().Resolve("esmping").Should().NotBeNull();
    }
}
