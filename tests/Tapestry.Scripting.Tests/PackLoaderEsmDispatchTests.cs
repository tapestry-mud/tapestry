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

    [Fact]
    public void LoadContent_NonEsmScriptsFormat_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();

        // Temp pack with scripts declared but scripts_format set to a non-esm value.
        var dir = Directory.CreateTempSubdirectory("tap-legacy-pack-").FullName;
        Directory.CreateDirectory(Path.Combine(dir, "dist", "scripts"));
        File.WriteAllText(Path.Combine(dir, "dist", "scripts", "init.js"), "// stub\n");
        File.WriteAllText(Path.Combine(dir, "pack.yaml"),
            "name: \"@tapestry/legacy-pack\"\nversion: \"0.0.1\"\ntype: module\n" +
            "content:\n  scripts: \"dist/scripts/**/*.js\"\n  scripts_format: legacy\n");

        var packLoader = provider.GetRequiredService<PackLoader>();
        provider.GetRequiredService<TapestryModuleLoader>()
            .Build(new Dictionary<string, string> { ["tapestry-legacy-pack"] = dir },
                   new HashSet<(string, string)>());
        var manifest = packLoader.LoadDeclarations(dir);

        var act = () => packLoader.LoadContent(dir, manifest);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*scripts_format*")
            .WithMessage("*esm*");
    }
}
