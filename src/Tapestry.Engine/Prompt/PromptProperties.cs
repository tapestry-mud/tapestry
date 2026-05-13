using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Prompt;

public static class PromptProperties
{
    public const string PromptTemplate = "prompt_template";

    public static void Register(PropertyRegistry registry)
    {
        registry.RegisterEngineProperty(PromptTemplate, "Custom prompt template string", PropertyValueType.String, appliesTo: new[] { "player" });
    }
}
