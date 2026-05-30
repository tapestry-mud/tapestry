using System.Threading.Tasks;
using Tapestry.Engine;
using Tapestry.Engine.Flow;
using Xunit;

namespace Tapestry.Engine.Tests;

public class FlowAsyncTests
{
    private static FlowInstance MakeInstance()
    {
        var entity = new Entity("player", "Rand");
        var def = new FlowDefinition
        {
            Id = "async_test_flow",
            Trigger = "test",
            Steps = new FlowStepDefinition[]
            {
                new ChoiceStep
                {
                    Id = "c",
                    Prompt = _ => "Choose:",
                    Options = _ => new[] { new ChoiceOption("Alpha", "a") },
                    OnSelect = (_, _) => { }
                }
            },
            OnComplete = _ => new FlowCompletionResult(true)
        };
        return new FlowInstance(def, entity);
    }

    [Fact]
    public async Task Instance_suspends_on_task_and_resumes_when_complete()
    {
        var tcs = new TaskCompletionSource<object?>();
        string? resumed = null;

        var instance = MakeInstance();
        instance.SuspendOnAsync(tcs.Task, result => resumed = result as string);

        Assert.True(instance.IsAwaitingAsync);
        Assert.False(instance.TryResumeAsync());   // not complete yet -> no-op
        Assert.Null(resumed);

        tcs.SetResult("hello");
        await Task.Yield();

        Assert.True(instance.TryResumeAsync());     // completes -> resume fires
        Assert.False(instance.IsAwaitingAsync);
        Assert.Equal("hello", resumed);
    }
}
