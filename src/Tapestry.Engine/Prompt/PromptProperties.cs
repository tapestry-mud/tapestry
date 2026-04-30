// See also: CommonProperties.cs for shared entity properties
using Tapestry.Engine.Persistence;

namespace Tapestry.Engine.Prompt;

/// <summary>
/// Property keys for the prompt system.
/// </summary>
public static class PromptProperties
{
    /// <summary>Key: "prompt_template"</summary>
    public const string PromptTemplate = "prompt_template";

    public static void Register(PropertyTypeRegistry registry)
    {
        registry.Register(PromptTemplate, typeof(string));
    }
}
