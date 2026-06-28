---
release: 0.1.44
specs: [area-authoring.md, scripting-runtime.md]
---

# Structured LLM Output

## Why

The recommend seam returned one free-text string and leaned on heuristic parsing
(whitespace-collapse, surrounding-quote-strip) to make it usable. That works for a
single name or description line, but a caller that wants several fields back at once -
a rolled mob with a name, a description, and stats - had to coax them out of prose and
re-split, a standing source of parse drift.

OpenAI-compatible structured outputs (`response_format: json_schema`) let the caller
hand the model a schema and get a validated JSON object back, so the pack parses JSON
instead of guessing at prose. It is opt-in: off by default, and a provider that ignores
the schema degrades to the caller's baked fallback (the pack-side JSON parse fails and
the pack uses its own value).

Token usage was also invisible. The provider now reads the response `usage` block and
surfaces prompt+completion token counts on the log line and a new metric, so the cost of
a fill run is observable.

## What

- **Structured-output recommend mode** (area-authoring.md). `OpenAiCompatibleLlmClient.CompleteAsync`
  takes an optional stringified JSON Schema. When it is non-empty AND the server flag
  `llm.structured_output` is true, the chat request attaches
  `response_format: { type: "json_schema", json_schema: { name, schema, strict: true } }`
  and the raw JSON content string is returned; the free-text `NormalizeSuggestion` step is
  bypassed (it would corrupt JSON). Flag off or no schema keeps the old free-text path.
  (src/Tapestry.Authoring/OpenAiCompatibleLlmClient.cs:23-60;
  src/Tapestry.Authoring/ILlmClient.cs:23;
  src/Tapestry.Authoring/LlmRecommendProvider.cs:53-70)

- **`llm.structured_output` config flag, default false** (area-authoring.md). New
  `LlmSection.StructuredOutput` on `ServerConfig`, mapped onto `RecommendLlmConfig.StructuredOutput`
  in the provider builder. Default off means no deployment changes behavior without opt-in.
  (src/Tapestry.Data/ServerConfig.cs:128;
  src/Tapestry.Scripting/ServiceCollectionExtensions.cs:259-270;
  src/Tapestry.Authoring/LlmRecommendProvider.cs:12-15)

- **Token usage capture** (area-authoring.md). `ILlmClient.CompleteAsync` now returns an
  `LlmResult` record (sanitized content + prompt/completion token counts) instead of a bare
  string; the provider reads the response `usage` block (0 when absent) and `RecommendResult`
  carries the totals. (src/Tapestry.Authoring/ILlmClient.cs:13,23;
  src/Tapestry.Authoring/OpenAiCompatibleLlmClient.cs:83-90;
  src/Tapestry.Engine/Recommend/IRecommendProvider.cs:8)

- **Token observability** (area-authoring.md). The recommend INFO log line gained a token
  field (e.g. `recommend[fill_mobs] ok 1731ms 293tok`), and a new histogram metric
  `tapestry.recommend.tokens` (prompt+completion per call, tagged by field and outcome)
  records alongside the existing recommend duration/calls metrics.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:235,245;
  src/Tapestry.Engine/TapestryMetrics.cs:134-137)

- **Schema-aware stub provider** (area-authoring.md). When a request carries a schema,
  `StaticStubRecommendProvider` returns a valid JSON instance generated from it via
  `StubJson.FromSchema`, so the structured path runs locally with no real LLM. The factory
  stub delay was lowered to 400ms (a solo fill run sequences several calls).
  (src/Tapestry.Engine/Recommend/StaticStubRecommendProvider.cs:31-34;
  src/Tapestry.Engine/Recommend/StubJson.cs:16-89;
  src/Tapestry.Authoring/LlmProviderFactory.cs:26)

- **Schema threading on the `authoring.recommend` binding** (scripting-runtime.md). The
  options bag gained an optional `schema` field (a stringified JSON Schema string) read from
  the JS object and threaded as `RecommendRequest.ResponseSchema`. Only strings cross the Jint
  boundary - the pack stringifies the schema in, the engine returns the model's content string
  out - so the engine never learns the pack's data shapes. The binding's string-return contract
  is unchanged in shape. (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:174-175,218;
  src/Tapestry.Engine/Recommend/IRecommendProvider.cs:6)
