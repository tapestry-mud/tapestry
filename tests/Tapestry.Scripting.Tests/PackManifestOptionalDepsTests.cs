using FluentAssertions;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class PackManifestOptionalDepsTests
{
    [Fact]
    public void LoadManifest_ParsesOptionalDependencies_SnakeCaseKey()
    {
        var yaml = """
            name: "@tapestry/cooking"
            version: "0.1.0"
            dependencies:
              "@tapestry/core": "^0.1.0"
            optional_dependencies:
              "@tapestry/survival": "^0.1.0"
            """;

        var manifest = YamlContentLoader.LoadManifest(yaml);

        manifest.Dependencies.Should().ContainKey("@tapestry/core");
        manifest.OptionalDependencies.Should().ContainKey("@tapestry/survival");
        manifest.OptionalDependencies["@tapestry/survival"].Should().Be("^0.1.0");
    }

    [Fact]
    public void LoadManifest_OptionalDependenciesDefaultsToEmpty_WhenAbsent()
    {
        var yaml = """
            name: "@tapestry/core"
            version: "0.1.0"
            """;

        var manifest = YamlContentLoader.LoadManifest(yaml);

        manifest.OptionalDependencies.Should().BeEmpty();
    }
}
