using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Registration;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class FlowsModule : IJintApiModule
{
    private readonly FlowRegistry _registry;
    private readonly FlowEngine _engine;
    private readonly SessionManager _sessions;
    private readonly RegistrationPolicy _registrationPolicy;

    public FlowsModule(FlowRegistry registry, FlowEngine engine, SessionManager sessions,
        RegistrationPolicy registrationPolicy)
    {
        _registry = registry;
        _engine = engine;
        _sessions = sessions;
        _registrationPolicy = registrationPolicy;
    }

    public string Namespace => "flows";

    public object Build(JintEngine jint)
    {
        return new
        {
            register = new Action<JsValue>(jsDefinition =>
            {
                var obj = (ObjectInstance)jsDefinition;
                var id = obj.Get("id").ToString();
                var trigger = obj.Get("trigger").ToString();

                var displayNameVal = obj.Get("display_name");
                var displayName = (displayNameVal.Type != Types.Undefined && displayNameVal.Type != Types.Null)
                    ? displayNameVal.ToString() : null;

                var packName = jint.CurrentPackOwner();
                var sourceFile = jint.CurrentSourceFile();

                var cancellableVal = obj.Get("cancellable");
                var cancellable = cancellableVal.Type == Types.Boolean && (bool)cancellableVal.ToObject()!;

                // Jint 4.7.1 has no IsBoolean; a missing JS field reads Undefined. Only Type==Boolean counts.
                var overrideVal = obj.Get("override");
                bool isOverride = overrideVal.Type == Types.Boolean && (bool)overrideVal.ToObject()!;

                var stepsVal = obj.Get("steps");
                var steps = ParseSteps(jint, stepsVal);

                var recommendContextKindVal = obj.Get("recommend_context");
                var recommendContextKind = (recommendContextKindVal.Type != Types.Undefined && recommendContextKindVal.Type != Types.Null)
                    ? recommendContextKindVal.ToString()
                    : null;

                var wizardStepsVal = obj.Get("wizard_steps");
                IReadOnlyList<WizardStep>? wizardSteps = null;
                if (wizardStepsVal is JsArray wizardStepsArr)
                {
                    var wsList = new List<WizardStep>();
                    for (uint wi = 0; wi < wizardStepsArr.Length; wi++)
                    {
                        var wsObj = (ObjectInstance)wizardStepsArr[(int)wi];
                        var wsId = wsObj.Get("id").ToString();
                        var wsLabel = wsObj.Get("label").ToString();
                        wsList.Add(new WizardStep(wsId, wsLabel));
                    }
                    wizardSteps = wsList;
                }

                var onCompleteJs = obj.Get("on_complete");
                Func<Entity, IFlowScratch, FlowCompletionResult> onComplete = (entity, scratch) =>
                {
                    if (onCompleteJs.Type == Types.Undefined || onCompleteJs.Type == Types.Null)
                    {
                        return new FlowCompletionResult(true);
                    }
                    var entityProxy = BuildEntityProxy(jint, entity, scratch);
                    var result = jint.Invoke(onCompleteJs, null, new object[] { entityProxy });

                    if (result is ObjectInstance resultObj)
                    {
                        var successVal = resultObj.Get("success");
                        var success = successVal.Type == Types.Boolean && (bool)successVal.ToObject()!;
                        var messageVal = resultObj.Get("message");
                        var message = (messageVal.Type != Types.Undefined && messageVal.Type != Types.Null)
                            ? messageVal.ToString() : null;
                        var suppressLookVal = resultObj.Get("suppress_look");
                        var suppressLook = suppressLookVal.Type == Types.Boolean && (bool)suppressLookVal.ToObject()!;
                        return new FlowCompletionResult(success, message, suppressLook);
                    }

                    return new FlowCompletionResult(true);
                };

                var flowDef = new FlowDefinition
                {
                    Id = id,
                    DisplayName = displayName,
                    Trigger = trigger,
                    Cancellable = cancellable,
                    Steps = steps,
                    OnComplete = onComplete,
                    PackName = packName,
                    WizardSteps = wizardSteps,
                    RecommendContextKind = recommendContextKind
                };

                // Declarative: the registry write replays at Resolve() (the seal barrier),
                // turning the silent last-wins clobber into a located boot error.
                _registrationPolicy.Record(new RegistrationCandidate(
                    Kind: "flow",
                    Name: id,
                    Owner: packName,
                    IsOverride: isOverride,
                    Commit: () => _registry.Register(flowDef),
                    SourceFile: sourceFile,
                    Line: 0));
            }),

            trigger = new Action<string, string, JsValue>((entityIdStr, triggerName, seedVal) =>
            {
                if (!Guid.TryParse(entityIdStr, out var entityId)) { return; }
                var session = _sessions.GetByEntityId(entityId);
                if (session == null) { return; }

                IReadOnlyDictionary<string, object?>? seed = null;
                if (seedVal.Type != Types.Undefined && seedVal.Type != Types.Null && seedVal is ObjectInstance seedObj)
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in seedObj.GetOwnProperties())
                    {
                        dict[prop.Key.ToString()] = prop.Value.Value.ToObject();
                    }
                    seed = dict;
                }

                _engine.Trigger(session, triggerName, seed);
            })
        };
    }

    private IReadOnlyList<FlowStepDefinition> ParseSteps(JintEngine jint, JsValue stepsVal)
    {
        var result = new List<FlowStepDefinition>();
        if (stepsVal is not JsArray arr) { return result; }

        for (uint i = 0; i < arr.Length; i++)
        {
            var stepObj = (ObjectInstance)arr[(int)i];
            var stepId = stepObj.Get("id").ToString();
            var stepType = stepObj.Get("type").ToString();

            Func<Entity, IFlowScratch, bool>? skipIf = null;
            var skipIfVal = stepObj.Get("skip_if");
            if (skipIfVal.Type != Types.Undefined && skipIfVal.Type != Types.Null)
            {
                var captured = skipIfVal;
                skipIf = (entity, scratch) =>
                {
                    var entityProxy = BuildEntityProxy(jint, entity, scratch);
                    var res = jint.Invoke(captured, null, new object[] { entityProxy });
                    return res.Type == Types.Boolean && (bool)res.ToObject()!;
                };
            }

            FlowStepDefinition step = stepType switch
            {
                "info" => ParseInfoStep(jint, stepId, stepObj, skipIf),
                "choice" => ParseChoiceStep(jint, stepId, stepObj, skipIf),
                "text" => ParseTextStep(jint, stepId, stepObj, skipIf),
                "confirm" => ParseConfirmStep(jint, stepId, stepObj, skipIf),
                _ => throw new InvalidOperationException($"Unknown flow step type: {stepType}")
            };

            result.Add(step);
        }

        return result;
    }

    private InfoStep ParseInfoStep(JintEngine jint, string id, ObjectInstance obj, Func<Entity, IFlowScratch, bool>? skipIf)
    {
        var textVal = obj.Get("text");
        Func<Entity, IFlowScratch, string> text;

        if (textVal.Type != Types.String)
        {
            var captured = textVal;
            text = (entity, scratch) =>
            {
                var entityProxy = BuildEntityProxy(jint, entity, scratch);
                return jint.Invoke(captured, null, new object[] { entityProxy }).ToString();
            };
        }
        else
        {
            var literal = textVal.ToString();
            text = (_, _) => literal;
        }

        return new InfoStep { Id = id, SkipIf = skipIf, Text = text };
    }

    private ChoiceStep ParseChoiceStep(JintEngine jint, string id, ObjectInstance obj, Func<Entity, IFlowScratch, bool>? skipIf)
    {
        var promptVal = obj.Get("prompt");
        Func<Entity, IFlowScratch, string> prompt;

        if (promptVal.Type != Types.String)
        {
            var captured = promptVal;
            prompt = (entity, scratch) =>
            {
                var entityProxy = BuildEntityProxy(jint, entity, scratch);
                return jint.Invoke(captured, null, new object[] { entityProxy }).ToString();
            };
        }
        else
        {
            var literal = promptVal.ToString();
            prompt = (_, _) => literal;
        }

        var optionsVal = obj.Get("options");
        Func<Entity, IFlowScratch, IReadOnlyList<ChoiceOption>> options;

        if (optionsVal is not JsArray)
        {
            var captured = optionsVal;
            options = (entity, scratch) =>
            {
                var entityProxy = BuildEntityProxy(jint, entity, scratch);
                var res = jint.Invoke(captured, null, new object[] { entityProxy });
                return ParseOptionsArray(jint, res);
            };
        }
        else
        {
            var staticOptions = ParseOptionsArray(jint, optionsVal);
            options = (_, _) => staticOptions;
        }

        var onSelectJs = obj.Get("on_select");
        Action<Entity, IFlowScratch, ChoiceOption> onSelect = (entity, scratch, option) =>
        {
            var entityProxy = BuildEntityProxy(jint, entity, scratch);
            var optionProxy = new { label = option.Label, value = option.Value };
            jint.Invoke(onSelectJs, null, new object[] { entityProxy, optionProxy });
        };

        var helpHintVal = obj.Get("help_hint");
        var helpHint = helpHintVal.Type == Types.String ? helpHintVal.ToString() : null;

        return new ChoiceStep { Id = id, SkipIf = skipIf, Prompt = prompt, Options = options, OnSelect = onSelect, HelpHint = helpHint };
    }

    private TextStep ParseTextStep(JintEngine jint, string id, ObjectInstance obj, Func<Entity, IFlowScratch, bool>? skipIf)
    {
        var promptVal = obj.Get("prompt");
        Func<Entity, IFlowScratch, string> prompt;

        if (promptVal.Type != Types.String)
        {
            var captured = promptVal;
            prompt = (entity, scratch) =>
            {
                var entityProxy = BuildEntityProxy(jint, entity, scratch);
                return jint.Invoke(captured, null, new object[] { entityProxy }).ToString();
            };
        }
        else
        {
            var literal = promptVal.ToString();
            prompt = (_, _) => literal;
        }

        Func<string, bool>? validate = null;
        var validateVal = obj.Get("validate");
        if (validateVal.Type != Types.Undefined && validateVal.Type != Types.Null)
        {
            var captured = validateVal;
            validate = input =>
            {
                var res = jint.Invoke(captured, null, new object[] { input });
                return res.Type == Types.Boolean && (bool)res.ToObject()!;
            };
        }

        var invalidMsgVal = obj.Get("invalid_message");
        var invalidMessage = (invalidMsgVal.Type != Types.Undefined && invalidMsgVal.Type != Types.Null)
            ? invalidMsgVal.ToString()
            : "Invalid input. Please try again.";

        var secretVal = obj.Get("secret");
        var secret = secretVal.Type == Types.Boolean && (bool)secretVal.ToObject()!;

        // recommend_field may be a literal string OR a function (entity) => fieldName, so a
        // generic field-picker flow can recommend the field the player just selected.
        var recommendFieldVal = obj.Get("recommend_field");
        Func<Entity, IFlowScratch, string?>? recommendField = null;
        if (recommendFieldVal.Type == Types.String)
        {
            var fieldName = recommendFieldVal.ToString();
            recommendField = (_, _) => fieldName;
        }
        else if (recommendFieldVal.Type != Types.Undefined && recommendFieldVal.Type != Types.Null)
        {
            var captured = recommendFieldVal;
            recommendField = (entity, scratch) =>
            {
                var entityProxy = BuildEntityProxy(jint, entity, scratch);
                var res = jint.Invoke(captured, null, new object[] { entityProxy });
                return res.Type == Types.String ? res.ToString() : null;
            };
        }

        var onInputJs = obj.Get("on_input");
        Action<Entity, IFlowScratch, string> onInput = (entity, scratch, value) =>
        {
            var entityProxy = BuildEntityProxy(jint, entity, scratch);
            jint.Invoke(onInputJs, null, new object[] { entityProxy, value });
        };

        return new TextStep
        {
            Id = id,
            SkipIf = skipIf,
            Prompt = prompt,
            Validate = validate,
            InvalidMessage = invalidMessage,
            OnInput = onInput,
            Secret = secret,
            RecommendField = recommendField
        };
    }

    private ConfirmStep ParseConfirmStep(JintEngine jint, string id, ObjectInstance obj, Func<Entity, IFlowScratch, bool>? skipIf)
    {
        var promptVal = obj.Get("prompt");
        Func<Entity, IFlowScratch, string> prompt;

        if (promptVal.Type != Types.String)
        {
            var captured = promptVal;
            prompt = (entity, scratch) =>
            {
                var entityProxy = BuildEntityProxy(jint, entity, scratch);
                return jint.Invoke(captured, null, new object[] { entityProxy }).ToString();
            };
        }
        else
        {
            var literal = promptVal.ToString();
            prompt = (_, _) => literal;
        }

        Action<Entity, IFlowScratch>? onYes = null;
        var onYesVal = obj.Get("on_yes");
        if (onYesVal.Type != Types.Undefined && onYesVal.Type != Types.Null)
        {
            var captured = onYesVal;
            onYes = (entity, scratch) =>
            {
                var entityProxy = BuildEntityProxy(jint, entity, scratch);
                jint.Invoke(captured, null, new object[] { entityProxy });
            };
        }

        Action<Entity, IFlowScratch>? onNo = null;
        var onNoVal = obj.Get("on_no");
        if (onNoVal.Type != Types.Undefined && onNoVal.Type != Types.Null)
        {
            var captured = onNoVal;
            onNo = (entity, scratch) =>
            {
                var entityProxy = BuildEntityProxy(jint, entity, scratch);
                jint.Invoke(captured, null, new object[] { entityProxy });
            };
        }

        return new ConfirmStep { Id = id, SkipIf = skipIf, Prompt = prompt, OnYes = onYes, OnNo = onNo };
    }

    private IReadOnlyList<ChoiceOption> ParseOptionsArray(JintEngine jint, JsValue val)
    {
        var list = new List<ChoiceOption>();
        if (val is not JsArray arr) { return list; }

        for (uint i = 0; i < arr.Length; i++)
        {
            var item = (ObjectInstance)arr[(int)i];
            var label = item.Get("label").ToString();
            var value = item.Get("value").ToObject();

            Func<Entity, IFlowScratch, string>? description = null;
            var descVal = item.Get("description");
            if (descVal.Type != Types.Undefined && descVal.Type != Types.Null)
            {
                if (descVal.Type == Types.String)
                {
                    var literal = descVal.ToString();
                    description = (_, _) => literal;
                }
                else
                {
                    var captured = descVal;
                    description = (entity, scratch) =>
                    {
                        var entityProxy = BuildEntityProxy(jint, entity, scratch);
                        return jint.Invoke(captured, null, new object[] { entityProxy }).ToString();
                    };
                }
            }

            var tagLineVal = item.Get("tag_line");
            var tagLine = (tagLineVal.Type != Types.Undefined && tagLineVal.Type != Types.Null)
                ? tagLineVal.ToString()
                : null;

            list.Add(new ChoiceOption(label, value, description, tagLine));
        }

        return list;
    }

    private object BuildEntityProxy(JintEngine jint, Entity entity, IFlowScratch scratch)
    {
        return new
        {
            id = entity.Id.ToString(),
            entityId = entity.Id.ToString(),
            name = entity.Name,
            roomId = entity.LocationRoomId,
            getProperty = new Func<string, object?>(key => entity.GetProperty<object>(key)),
            setProperty = new Action<string, object?>((key, value) =>
            {
                if (value != null) { entity.SetProperty(key, value); }
            }),
            scratch = new
            {
                get = new Func<string, object?>(key =>
                    scratch.Has(key) ? JsValue.FromObject(jint, scratch.Get(key)) : JsValue.Undefined),
                set = new Action<string, object?>((key, value) => scratch.Set(key, value)),
                has = new Func<string, bool>(key => scratch.Has(key))
            },
            send = new Action<string>(text =>
            {
                var session = _sessions.GetByEntityId(entity.Id);
                if (session != null) { session.SendLine(text.TrimEnd('\r', '\n')); }
            })
        };
    }
}
