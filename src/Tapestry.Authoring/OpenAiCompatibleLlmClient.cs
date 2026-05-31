using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Tapestry.Authoring;

/// <summary>One-shot chat completion over the OpenAI-compatible /chat/completions endpoint.
/// Works for Ollama, OpenAI, and Anthropic's OpenAI-compatible API by config alone.
/// No streaming, retries, or tool-calling (the seam is one-shot).</summary>
public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private readonly HttpClient _http;

    public OpenAiCompatibleLlmClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> CompleteAsync(string system, string user, LlmOptions opts, CancellationToken ct = default)
    {
        var url = $"{opts.BaseUrl.TrimEnd('/')}/chat/completions";
        var payload = new
        {
            model = opts.Model,
            temperature = opts.Temperature,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        if (!string.IsNullOrEmpty(opts.ApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);
        }

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return OutputSanitizer.Clean(content);
    }
}
