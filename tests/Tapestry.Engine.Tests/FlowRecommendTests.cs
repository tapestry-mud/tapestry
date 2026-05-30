using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Recommend;
using Xunit;

namespace Tapestry.Engine.Tests;

public class FlowRecommendTests
{
    private const string FirstDescription =
        "Moss carpets the stones underfoot, and the air hangs cool and still.";

    private static (FlowInstance instance, PlayerSession session, FakeConnection conn) Setup(
        RecommendBroker broker, out List<string> captured)
    {
        var capturedList = new List<string>();
        captured = capturedList;

        var entity = new Entity("player", "Rand");
        var conn = new FakeConnection();
        var session = new PlayerSession(conn, entity);

        var def = new FlowDefinition
        {
            Id = "recommend_test_flow",
            Trigger = "test",
            Steps = new FlowStepDefinition[]
            {
                new TextStep
                {
                    Id = "desc",
                    RecommendField = "description",
                    Prompt = _ => "Enter the room description:",
                    OnInput = (_, val) => { capturedList.Add(val); }
                }
            },
            OnComplete = _ => new FlowCompletionResult(true)
        };

        var instance = new FlowInstance(def, entity, recommend: broker, recommendContext: _ => new RoomData());
        return (instance, session, conn);
    }

    /// <summary>Spin (bounded) until the pending async completes and the resume fires.</summary>
    private static async Task ResumeWhenReady(FlowInstance instance)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5000)
        {
            if (instance.TryResumeAsync())
            {
                return;
            }
            await Task.Delay(10);
        }
        throw new Xunit.Sdk.XunitException("Recommend async never completed within the timeout.");
    }

    [Fact]
    public async Task Recommend_renders_numbered_suggestions_and_selection_feeds_OnInput()
    {
        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));
        var (instance, session, conn) = Setup(broker, out var captured);

        instance.Start(session);

        // The literal "recommend" triggers the side-action and suspends on the async.
        instance.HandleInput("recommend");

        await ResumeWhenReady(instance);

        // Three description suggestions rendered as a numbered list.
        conn.SentText.Should().Contain(s => s.Contains("Suggestions:"));
        conn.SentText.Should().Contain(s => s.Contains("1. " + FirstDescription));

        // Picking "1" feeds the first suggestion to the step's OnInput.
        instance.HandleInput("1");

        captured.Should().ContainSingle().Which.Should().Be(FirstDescription);
    }
}
