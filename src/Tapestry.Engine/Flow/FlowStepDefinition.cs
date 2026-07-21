namespace Tapestry.Engine.Flow;

public abstract class FlowStepDefinition
{
    public required string Id { get; init; }
    public Func<Entity, IFlowScratch, bool>? SkipIf { get; init; }

    /// <summary>If set, resolves (per entity + scratch) the room field this step's "recommend"
    /// side-action should suggest for — evaluated when the player types "~".
    /// Returns null/empty to disable recommend for the current field. A resolver lets a
    /// generic field-picker flow recommend the field the player actually selected.</summary>
    public Func<Entity, IFlowScratch, string?>? RecommendField { get; init; }
}

public class InfoStep : FlowStepDefinition
{
    public required Func<Entity, IFlowScratch, string> Text { get; init; }
}

public record ChoiceOption(string Label, object? Value, Func<Entity, IFlowScratch, string>? Description = null, string? TagLine = null);

public class ChoiceStep : FlowStepDefinition
{
    public required Func<Entity, IFlowScratch, string> Prompt { get; init; }
    public required Func<Entity, IFlowScratch, IReadOnlyList<ChoiceOption>> Options { get; init; }
    public required Action<Entity, IFlowScratch, ChoiceOption> OnSelect { get; init; }
    public string? HelpHint { get; init; }
}

public class TextStep : FlowStepDefinition
{
    public required Func<Entity, IFlowScratch, string> Prompt { get; init; }
    public Func<string, bool>? Validate { get; init; }
    public string InvalidMessage { get; init; } = "Invalid input. Please try again.";
    public required Action<Entity, IFlowScratch, string> OnInput { get; init; }
    public bool Secret { get; init; } = false;
}

public class ConfirmStep : FlowStepDefinition
{
    public required Func<Entity, IFlowScratch, string> Prompt { get; init; }
    public Action<Entity, IFlowScratch>? OnYes { get; init; }
    public Action<Entity, IFlowScratch>? OnNo { get; init; }
}

public record FlowCompletionResult(bool Success, string? Message = null, bool SuppressLook = false);
