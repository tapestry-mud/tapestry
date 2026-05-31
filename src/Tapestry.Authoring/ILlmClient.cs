using System.Threading;
using System.Threading.Tasks;

namespace Tapestry.Authoring;

/// <summary>Per-call connection + model parameters. The API key is resolved from the
/// environment by the composition layer (never from YAML) and passed in here.</summary>
public sealed record LlmOptions(
    string Model, double Temperature, int TimeoutSeconds, string BaseUrl, string ApiKey);

/// <summary>Provider-agnostic one-shot chat completion. Works for Ollama, OpenAI, and
/// Anthropic's OpenAI-compatible endpoint by config alone.</summary>
public interface ILlmClient
{
    /// <summary>One chat completion. Returns sanitized assistant text (see OutputSanitizer).</summary>
    Task<string> CompleteAsync(string system, string user, LlmOptions opts, CancellationToken ct = default);
}
