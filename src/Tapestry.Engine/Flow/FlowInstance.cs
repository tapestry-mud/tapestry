using Tapestry.Engine.Ui;

namespace Tapestry.Engine.Flow;

public class FlowInstance
{
    private const int WizardPanelWidth = 47;

    private readonly FlowDefinition _definition;
    private readonly Entity _entity;
    private readonly PanelRenderer _panelRenderer;
    private PlayerSession? _session;
    private int _currentStepIndex;

    public Action? OnCompleted { get; set; }
    public FlowDefinition Definition => _definition;
    public Entity Entity => _entity;
    public int CurrentStepIndex => _currentStepIndex;

    public FlowInstance(FlowDefinition definition, Entity entity, PanelRenderer? panelRenderer = null)
    {
        _definition = definition;
        _entity = entity;
        _panelRenderer = panelRenderer ?? new PanelRenderer();
        _currentStepIndex = -1;
    }

    public void Start(PlayerSession session)
    {
        _session = session;
        Advance();
    }

    public void HandleInput(string input)
    {
        if (_currentStepIndex < 0 || _currentStepIndex >= _definition.Steps.Count)
        {
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

        if (lower == "?" || lower == "help")
        {
            _session!.SendLine("Type ? [option] or ? [number] to learn more about a choice.");
            if (_definition.WizardSteps == null || !_session!.Connection.SupportsAnsi)
            {
                RenderCurrentStep();
            }
            return;
        }

        string? helpTarget = null;
        if (trimmed.StartsWith("? "))
        {
            helpTarget = trimmed[2..].Trim();
        }
        else if (lower.StartsWith("help "))
        {
            helpTarget = trimmed[5..].Trim();
        }

        if (helpTarget != null)
        {
            var opts = step.Options(_entity);
            ChoiceOption? match;

            if (int.TryParse(helpTarget, out var helpNum) && helpNum >= 1 && helpNum <= opts.Count)
            {
                match = opts[helpNum - 1];
            }
            else
            {
                var targetLower = helpTarget.ToLowerInvariant();
                match = opts.FirstOrDefault(o => o.Label.ToLowerInvariant().StartsWith(targetLower));
            }

            if (match == null)
            {
                _session!.SendLine($"Unknown option: {helpTarget}");
            }
            else if (match.Description != null)
            {
                _session!.SendLine(match.Description(_entity));
            }
            else
            {
                _session!.SendLine($"No additional information available for {match.Label}.");
            }
            if (_definition.WizardSteps == null || !_session!.Connection.SupportsAnsi)
            {
                RenderCurrentStep();
            }
            return;
        }

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
                _session!.SendLine(info.Text(_entity));
                break;
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
                    _session!.SendLine("  ? [option] or ? [number] for details");
                }
                break;
            }
            case TextStep text:
                if (text.Secret)
                {
                    _session!.Connection.SuppressEcho();
                }
                _session!.SendLine(text.Prompt(_entity));
                break;
            case ConfirmStep confirm:
                _session!.SendLine($"{confirm.Prompt(_entity)} (y/n)");
                break;
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
                    new FooterRow { Content = "? [option] or ? [number] for details" }
                }
            });
        }

        return new Panel { Width = WizardPanelWidth, Sections = sections };
    }
}
