namespace Tapestry.Engine.Flow;

public class FlowDefinition
{
    public required string Id { get; init; }
    public string? DisplayName { get; init; }
    public required string Trigger { get; init; }
    public required IReadOnlyList<FlowStepDefinition> Steps { get; init; }
    public required Func<Entity, FlowCompletionResult> OnComplete { get; init; }
    public string PackName { get; init; } = "";
    public IReadOnlyList<WizardStep>? WizardSteps { get; init; }
}
