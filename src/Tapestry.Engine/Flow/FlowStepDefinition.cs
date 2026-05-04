namespace Tapestry.Engine.Flow;

public abstract class FlowStepDefinition
{
    public required string Id { get; init; }
    public Func<Entity, bool>? SkipIf { get; init; }
}

public class InfoStep : FlowStepDefinition
{
    public required Func<Entity, string> Text { get; init; }
}

public record ChoiceOption(string Label, object? Value, Func<Entity, string>? Description = null, string? TagLine = null);

public class ChoiceStep : FlowStepDefinition
{
    public required Func<Entity, string> Prompt { get; init; }
    public required Func<Entity, IReadOnlyList<ChoiceOption>> Options { get; init; }
    public required Action<Entity, ChoiceOption> OnSelect { get; init; }
    public string? HelpHint { get; init; }
}

public class TextStep : FlowStepDefinition
{
    public required Func<Entity, string> Prompt { get; init; }
    public Func<string, bool>? Validate { get; init; }
    public string InvalidMessage { get; init; } = "Invalid input. Please try again.";
    public required Action<Entity, string> OnInput { get; init; }
    public bool Secret { get; init; } = false;
}

public class ConfirmStep : FlowStepDefinition
{
    public required Func<Entity, string> Prompt { get; init; }
    public Action<Entity>? OnYes { get; init; }
    public Action<Entity>? OnNo { get; init; }
}

public record FlowCompletionResult(bool Success, string? Message = null);
