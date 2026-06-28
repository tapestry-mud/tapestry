using FluentAssertions;
using Tapestry.Authoring;
using Tapestry.Data;
using Tapestry.Engine.Recommend;
using Tapestry.Scripting;
using Xunit;

namespace Tapestry.Scripting.Tests;

public class RecommendCompositionTests
{
    [Fact]
    public void Enabled_config_builds_the_llm_provider()
    {
        var llm = new LlmSection { Enabled = true };
        var provider = ServiceCollectionExtensions.BuildRecommendProvider(llm, _ => null);
        provider.Should().BeOfType<LlmRecommendProvider>();
    }

    [Fact]
    public void Disabled_config_without_stub_builds_nothing()
    {
        var llm = new LlmSection { Enabled = false, UseStub = false };
        ServiceCollectionExtensions.BuildRecommendProvider(llm, _ => null).Should().BeNull();
    }

    [Fact]
    public void Disabled_with_use_stub_builds_the_stub()
    {
        var llm = new LlmSection { Enabled = false, UseStub = true };
        ServiceCollectionExtensions.BuildRecommendProvider(llm, _ => null)
            .Should().BeOfType<StaticStubRecommendProvider>();
    }

    [Fact]
    public void StructuredOutput_flag_maps_through_without_breaking_composition()
    {
        var llm = new LlmSection { Enabled = true, StructuredOutput = true };
        var provider = ServiceCollectionExtensions.BuildRecommendProvider(llm, _ => null);
        provider.Should().BeOfType<LlmRecommendProvider>();
        provider!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Requires_key_provider_is_disabled_when_env_key_missing_but_enabled_when_present()
    {
        var llm = new LlmSection { Enabled = true, RequiresKey = true, ApiKeyEnv = "MY_KEY" };

        var missing = ServiceCollectionExtensions.BuildRecommendProvider(llm, _ => null);
        missing!.IsEnabled.Should().BeFalse();

        var present = ServiceCollectionExtensions.BuildRecommendProvider(llm, name => name == "MY_KEY" ? "sk-abc" : null);
        present!.IsEnabled.Should().BeTrue();
    }
}
