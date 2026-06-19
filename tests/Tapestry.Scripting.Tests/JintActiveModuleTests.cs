using Jint;
using Jint.Native;
using FluentAssertions;
using Tapestry.Scripting; // JintActiveModule lives in the parent namespace; not auto-imported

namespace Tapestry.Scripting.Tests;

public class JintActiveModuleTests
{
    [Fact]
    public void ActiveLocation_InsideImportedModule_ReturnsModuleLocation()
    {
        string? seen = null;
        var engine = new Jint.Engine(o => o.EnableModules(new InlineLoader()));
        engine.Modules.Add("probe", b => b.ExportFunction(
            "whereAmI", _ => { seen = JintActiveModule.ActiveLocation(engine); return JsValue.Undefined; }));
        // InlineLoader maps "fixture" -> a module whose location is "pack:test::a.js"
        engine.Modules.Import("fixture");

        seen.Should().Be("pack:test::a.js");
    }

    // Minimal loader: "probe" is the builder module; "fixture" is one source module
    // whose ResolvedSpecifier.Key (and therefore Location) is "pack:test::a.js".
    private sealed class InlineLoader : Jint.Runtime.Modules.ModuleLoader
    {
        public override Jint.Runtime.Modules.ResolvedSpecifier Resolve(
            string? referencingModuleLocation, Jint.Runtime.Modules.ModuleRequest moduleRequest)
        {
            var spec = moduleRequest.Specifier;
            var key = spec == "fixture" ? "pack:test::a.js" : spec;
            return new Jint.Runtime.Modules.ResolvedSpecifier(
                moduleRequest, key, null, Jint.Runtime.Modules.SpecifierType.Bare);
        }

        protected override string LoadModuleContents(
            Jint.Engine engine, Jint.Runtime.Modules.ResolvedSpecifier resolved)
            => "import { whereAmI } from \"probe\";\nwhereAmI();\n";
    }
}
