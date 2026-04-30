using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class DataModuleTests
{
    private (JintRuntime rt, PackContext packContext) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<PackContext>());
    }

    [Fact]
    public void LoadYaml_ReturnsArray_FromValidFile()
    {
        var (rt, packContext) = BuildRuntime();

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "socials");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "test.yaml"), """
- name: smile
  no_target: "You smile happily."
- name: nod
  no_target: "You nod solemnly."
""");

        try
        {
            packContext.CurrentPackDir = tempDir;

            var count = rt.Evaluate("tapestry.data.loadYaml('socials/test.yaml').length");

            count.Should().Be(2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LoadYaml_ReturnsNull_WhenFileNotFound()
    {
        var (rt, packContext) = BuildRuntime();
        packContext.CurrentPackDir = Path.GetTempPath();

        var result = rt.Evaluate("tapestry.data.loadYaml('nonexistent/file.yaml')");

        result.Should().BeNull();
    }

    [Fact]
    public void LoadYaml_BlocksPathTraversal()
    {
        var (rt, packContext) = BuildRuntime();
        packContext.CurrentPackDir = Path.GetTempPath();

        var result = rt.Evaluate("tapestry.data.loadYaml('../../../etc/passwd')");

        result.Should().BeNull();
    }
}
