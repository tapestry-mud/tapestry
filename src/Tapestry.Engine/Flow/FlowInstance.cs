using Tapestry.Engine.Authoring;
using Tapestry.Engine.Recommend;
using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Flow;

public class FlowInstance
{
    private const int WizardPanelWidth = 47;

    private readonly FlowDefinition _definition;
    private readonly Entity _entity;
    private readonly PanelRenderer _panelRenderer;
    private readonly RecommendBroker? _recommend;
    private readonly Func<Entity, IRecommendContext>? _recommendContext;
    private PlayerSession? _session;
    private int _currentStepIndex;
    private bool _awaitingEmptyAck;
    private Task<object?>? _pendingAsync;
    private Action<object?>? _onAsyncComplete;
    private IReadOnlyList<string>? _pendingSuggestions;

    public Action? OnCompleted { get; set; }
    public Action<string>? OnAborted { get; set; }
    public Action<string, string, object>? GmcpSend { get; set; }
    public Action<string>? CommandFallback { get; set; }
    public FlowDefinition Definition => _definition;
    public Entity Entity => _entity;
    public int CurrentStepIndex => _currentStepIndex;

    public FlowInstance(
        FlowDefinition definition,
        Entity entity,
        PanelRenderer? panelRenderer = null,
        RecommendBroker? recommend = null,
        Func<Entity, IRecommendContext>? recommendContext = null)
    {
        _definition = definition;
        _entity = entity;
        _panelRenderer = panelRenderer ?? new PanelRenderer();
        _recommend = recommend;
        _recommendContext = recommendContext;
        _currentStepIndex = -1;
    }

    public void Start(PlayerSession session)
    {
        _session = session;
        Advance();
    }

    /// <summary>True while the flow is paused awaiting an async result (not player input).</summary>
    public bool IsAwaitingAsync => _pendingAsync is { IsCompleted: false };

    /// <summary>Suspend the flow on an arbitrary async result. The continuation runs on the
    /// game thread when <see cref="TryResumeAsync"/> observes completion.</summary>
    public void SuspendOnAsync(Task<object?> pending, Action<object?> onComplete)
    {
        _pendingAsync = pending;
        _onAsyncComplete = onComplete;
    }

    /// <summary>Called each tick by the resume sweep. Returns true if a resume fired this call.
    /// Safe on the game thread: only reads Task.IsCompleted/Result here.</summary>
    public bool TryResumeAsync()
    {
        if (_pendingAsync == null || !_pendingAsync.IsCompleted)
        {
            return false;
        }
        var pending = _pendingAsync;
        var onComplete = _onAsyncComplete;
        _pendingAsync = null;
        _onAsyncComplete = null;

        object? result = null;
        if (pending.Status == TaskStatus.RanToCompletion)
        {
            result = pending.Result;
        }
        onComplete?.Invoke(result); // result == null signals failure/cancellation; caller renders gracefully
        return true;
    }

    public void HandleInput(string input)
    {
        if (IsAwaitingAsync)
        {
            return;
        }

        if (_currentStepIndex < 0 || _currentStepIndex >= _definition.Steps.Count)
        {
            return;
        }

        var trimmed = input.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (lower == "?" || lower == "help")
        {
            CommandFallback?.Invoke("help");
            return;
        }
        if (trimmed.StartsWith("? "))
        {
            CommandFallback?.Invoke("help " + trimmed[2..].Trim());
            return;
        }
        if (lower.StartsWith("help "))
        {
            CommandFallback?.Invoke(trimmed);
            return;
        }

        if (_awaitingEmptyAck)
        {
            _awaitingEmptyAck = false;
            _currentStepIndex = _definition.Steps.Count; // terminate this instance; ignore further input
            OnAborted?.Invoke("empty_choice");
            return;
        }

        var step = _definition.Steps[_currentStepIndex];

        switch (step)
        {
            case ChoiceStep choice:
                HandleChoiceInput(choice, input);
                break;
            case TextStep text:
                HandleTextInput(text, input);
                break;
            case ConfirmStep confirm:
                HandleConfirmInput(confirm, input);
                break;
        }
    }

    private void HandleChoiceInput(ChoiceStep step, string input)
    {
        var trimmed = input.Trim();
        var lower = trimmed.ToLowerInvariant();
        var options = step.Options(_entity);
        ChoiceOption? chosen = null;

        if (int.TryParse(trimmed, out var num) && num >= 1 && num <= options.Count)
        {
            chosen = options[num - 1];
        }
        else
        {
            chosen = options.FirstOrDefault(o => o.Label.ToLowerInvariant().StartsWith(lower));
        }

        if (chosen == null)
        {
            _session!.SendLine("Invalid choice. Enter a number or the beginning of a choice name.");
            RenderCurrentStep();
            return;
        }

        step.OnSelect(_entity, chosen);
        Advance();
    }

    private void HandleTextInput(TextStep step, string input)
    {
        // (1) A multi-suggestion selection is pending? A list index picks a suggestion;
        // anything else is honored as the player's own typed value (the prompt promises
        // "or type your own value"), falling through to the normal validate/OnInput path.
        if (_pendingSuggestions != null)
        {
            if (int.TryParse(input.Trim(), out var pick) && pick >= 1 && pick <= _pendingSuggestions.Count)
            {
                var chosen = _pendingSuggestions[pick - 1];
                _pendingSuggestions = null;
                step.OnInput(_entity, chosen);
                Advance();
                return;
            }
            _pendingSuggestions = null;
        }

        // (2) Recommend side-action — resolves the field per-entity so a generic field-picker
        // recommends the field the player actually selected. The "~" sigil is the ONLY trigger;
        // text after "~" (trimmed) is the author's intent hint. Anything not starting with "~"
        // is a literal field value.
        var trimmed = input.Trim();
        var isRecommend = trimmed.StartsWith("~");
        if (step.RecommendField != null && _recommend != null && _recommendContext != null && isRecommend)
        {
            if (!_recommend.IsEnabled)
            {
                _session?.SendLine("Recommendation is currently unavailable. Enter a value:");
                return;
            }
            var field = step.RecommendField(_entity);
            if (string.IsNullOrEmpty(field))
            {
                // The step opted into recommend, but not for THIS field (edit-room only
                // recommends name/description). Don't silently set the field to "~".
                _session?.SendLine("Recommendation isn't available for this field. Enter a value:");
                return;
            }
            var rest = trimmed[1..].Trim();
            var hint = string.IsNullOrWhiteSpace(rest) ? null : rest;
            var request = new RecommendRequest(field, _recommendContext(_entity), hint);
            _session?.SendLine("Thinking...");
            SuspendOnAsync(
                _recommend.RecommendAsync(request).ContinueWith(t =>
                    t.Status == TaskStatus.RanToCompletion ? (object?)t.Result : null),
                result => OnRecommendComplete(step, result as RecommendResult));
            return;
        }

        if (step.Secret)
        {
            _session!.Connection.RestoreEcho();
            _session!.SendLine("");
        }

        if (step.Validate != null && !step.Validate(input))
        {
            _session!.SendLine(step.InvalidMessage);
            if (step.Secret)
            {
                _session!.Connection.SuppressEcho();
            }
            _session!.SendLine(step.Prompt(_entity));
            return;
        }

        step.OnInput(_entity, input);
        Advance();
    }

    private void OnRecommendComplete(TextStep step, RecommendResult? result)
    {
        var suggestions = result?.Suggestions;
        if (suggestions == null || suggestions.Count == 0)
        {
            _session?.SendLine("No suggestions available. Enter a value:");
            return;
        }
        if (suggestions.Count == 1)
        {
            step.OnInput(_entity, suggestions[0]);
            Advance();
            return;
        }
        _pendingSuggestions = suggestions;
        var sb = new System.Text.StringBuilder("Suggestions:\r\n");
        for (var i = 0; i < suggestions.Count; i++)
        {
            sb.Append($"  {i + 1}. {suggestions[i]}\r\n");
        }
        sb.Append("Pick a number, or type your own value:");
        _session?.SendLine(sb.ToString());
    }

    private void HandleConfirmInput(ConfirmStep step, string input)
    {
        var lower = input.Trim().ToLowerInvariant();

        if (lower == "y" || lower == "yes")
        {
            step.OnYes?.Invoke(_entity);
            Advance();
        }
        else if (lower == "n" || lower == "no")
        {
            step.OnNo?.Invoke(_entity);
            Advance();
        }
        else
        {
            _session!.SendLine("Please enter y or n.");
            RenderCurrentStep();
        }
    }

    private void Advance()
    {
        do
        {
            _currentStepIndex++;
            if (_currentStepIndex >= _definition.Steps.Count)
            {
                OnCompleted?.Invoke();
                return;
            }
        }
        while (_definition.Steps[_currentStepIndex].SkipIf?.Invoke(_entity) == true);

        // #20: a required choice with zero eligible options is a content misconfiguration,
        // not a valid state (legitimately-skippable steps use SkipIf). Surface it loudly
        // and abort rather than render an impossible prompt the player can never satisfy.
        if (_definition.Steps[_currentStepIndex] is ChoiceStep emptyCheck
            && emptyCheck.Options(_entity).Count == 0)
        {
            var message = emptyCheck.HelpHint != null
                ? $"There are no {emptyCheck.HelpHint} to choose from."
                : "There are no options to choose from.";
            _session!.SendLine(message);
            _session!.SendLine("Press Enter to continue.");
            GmcpSend?.Invoke(_session!.Connection.Id, "Flow.Step", new { type = "info", prompt = message });
            _awaitingEmptyAck = true;
            return;
        }

        RenderCurrentStep();

        if (_definition.Steps[_currentStepIndex] is InfoStep)
        {
            Advance();
        }
    }

    private void RenderCurrentStep()
    {
        var step = _definition.Steps[_currentStepIndex];

        switch (step)
        {
            case InfoStep info:
            {
                var infoText = info.Text(_entity);
                _session!.SendLine(infoText);
                GmcpSend?.Invoke(_session.Connection.Id, "Flow.Step", new { type = "info", prompt = infoText });
                break;
            }
            case ChoiceStep choice:
            {
                if (_definition.WizardSteps != null && _session!.Connection.SupportsAnsi)
                {
                    RenderWizardPanel(choice);
                }
                else
                {
                    _session!.SendLine(choice.Prompt(_entity));
                    var options = choice.Options(_entity);
                    for (var i = 0; i < options.Count; i++)
                    {
                        _session!.SendLine($"  {i + 1}. {options[i].Label}");
                    }
                    var hint = choice.HelpHint != null ? $"help {choice.HelpHint}" : "help [topic]";
                    _session!.SendLine($"  Type {hint} for details");
                }
                var choiceOptions = choice.Options(_entity);
                var choicePayload = choiceOptions.Select(o => o.TagLine != null
                    ? (object)new { label = o.Label, tagLine = o.TagLine }
                    : (object)new { label = o.Label }).ToArray();
                GmcpSend?.Invoke(_session!.Connection.Id, "Flow.Step", new { type = "choice", prompt = choice.Prompt(_entity), options = choicePayload });
                break;
            }
            case TextStep text:
            {
                if (text.Secret)
                {
                    _session!.Connection.SuppressEcho();
                }
                var textPrompt = text.Prompt(_entity);
                _session!.SendLine(textPrompt);
                GmcpSend?.Invoke(_session!.Connection.Id, "Flow.Step", new { type = "text", prompt = textPrompt });
                break;
            }
            case ConfirmStep confirm:
            {
                var confirmPrompt = confirm.Prompt(_entity);
                _session!.SendLine($"{confirmPrompt} (y/n)");
                GmcpSend?.Invoke(_session!.Connection.Id, "Flow.Step", new { type = "confirm", prompt = confirmPrompt });
                break;
            }
        }
    }

    private string FormatProgressRow(IReadOnlyList<WizardStep> wizardSteps)
    {
        const int progressContentWidth = 43;

        var stepIndexById = _definition.Steps
            .Select((s, i) => (s.Id, i))
            .ToDictionary(x => x.Id, x => x.i);

        var parts = new List<string>();
        var currentWizardIndex = -1;

        for (var wi = 0; wi < wizardSteps.Count; wi++)
        {
            var ws = wizardSteps[wi];
            string marker;
            if (!stepIndexById.TryGetValue(ws.StepId, out var stepIndex) || stepIndex > _currentStepIndex)
            {
                marker = "[ ]";
            }
            else if (stepIndex == _currentStepIndex)
            {
                marker = "[>]";
                currentWizardIndex = wi;
            }
            else
            {
                marker = "[*]";
            }
            parts.Add($"{ws.Label} {marker}");
        }

        var full = string.Join("   ", parts);
        if (full.Length <= progressContentWidth)
        {
            return full;
        }

        var displayIndex = currentWizardIndex >= 0 ? currentWizardIndex + 1 : wizardSteps.Count;
        var currentLabel = currentWizardIndex >= 0 ? wizardSteps[currentWizardIndex].Label : wizardSteps[^1].Label;
        return $"Step {displayIndex} of {wizardSteps.Count}: {currentLabel}";
    }

    private void RenderWizardPanel(ChoiceStep step)
    {
        { _session!.Connection.ClearScreen(); }
        { _session!.SendLine(_panelRenderer.Render(BuildWizardPanel(step))); }
    }

    private Panel BuildWizardPanel(ChoiceStep step)
    {
        var sections = new List<Section>();

        if (_definition.WizardSteps != null)
        {
            {
                sections.Add(new Section
                {
                    Rows = new Row[]
                    {
                        new EmptyRow(),
                        new TextRow { Content = " " + FormatProgressRow(_definition.WizardSteps) },
                        new EmptyRow()
                    }
                });
            }
        }

        var choiceRows = new List<Row>
        {
            new EmptyRow(),
            new TextRow { Content = " " + step.Prompt(_entity) },
            new EmptyRow()
        };

        var options = step.Options(_entity);
        {
            for (var i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                var line = opt.TagLine != null
                    ? $"{i + 1}. {opt.Label} - {opt.TagLine}"
                    : $"{i + 1}. {opt.Label}";
                choiceRows.Add(new TextRow { Content = " " + line });
            }
        }
        { choiceRows.Add(new EmptyRow()); }

        {
            sections.Add(new Section
            {
                SeparatorAbove = _definition.WizardSteps != null ? RuleStyle.Minor : RuleStyle.None,
                Rows = choiceRows
            });
        }

        {
            sections.Add(new Section
            {
                SeparatorAbove = RuleStyle.Major,
                Rows = new Row[]
                {
                    new FooterRow { Content = step.HelpHint != null ? $"Type help {step.HelpHint} for details" : "Type help [topic] for details" }
                }
            });
        }

        return new Panel { Width = WizardPanelWidth, Sections = sections };
    }
}
