using FluentAssertions;
using Tapestry.Data;

namespace Tapestry.Server.Tests;

public class ServerConfigLlmTests
{
    [Fact]
    public void Llm_block_deserializes_snake_case_keys_and_defaults_off()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tapestry-llm-cfg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "server.yaml");
        File.WriteAllText(path, """
            llm:
              enabled: true
              use_stub: false
              base_url: "http://localhost:11434/v1"
              model: "qwen2.5:7b"
              api_key_env: "TAPESTRY_LLM_API_KEY"
              requires_key: false
              temperature: 0.7
              max_sentences: 3
              candidates: 4
              timeout_seconds: 20
              system_prompt: "You are terse."
            """);

        try
        {
            var config = ServerConfig.Load(path);

            config.Llm.Enabled.Should().BeTrue();
            config.Llm.UseStub.Should().BeFalse();
            config.Llm.BaseUrl.Should().Be("http://localhost:11434/v1");
            config.Llm.Model.Should().Be("qwen2.5:7b");
            config.Llm.ApiKeyEnv.Should().Be("TAPESTRY_LLM_API_KEY");
            config.Llm.RequiresKey.Should().BeFalse();
            config.Llm.Temperature.Should().Be(0.7);
            config.Llm.MaxSentences.Should().Be(3);
            config.Llm.Candidates.Should().Be(4);
            config.Llm.TimeoutSeconds.Should().Be(20);
            config.Llm.SystemPrompt.Should().Be("You are terse.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Llm_defaults_to_disabled_when_absent()
    {
        new ServerConfig().Llm.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Llm_neighbor_guidance_deserializes_from_snake_case()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tapestry-llm-cfg-ng-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "server.yaml");
        File.WriteAllText(path, """
            llm:
              enabled: true
              neighbor_guidance: "Stay on theme, but make it yours."
            """);

        try
        {
            var config = ServerConfig.Load(path);
            config.Llm.NeighborGuidance.Should().Be("Stay on theme, but make it yours.");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
