using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Flow;
using Tapestry.Engine.Recommend;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Xunit;

namespace Tapestry.Engine.Tests;

public class FlowRecommendTests
{
    private const string FirstDescription =
        "Moss carpets the stones underfoot, and the air hangs cool and still.";

    private static (FlowInstance instance, PlayerSession session, FakeConnection conn) Setup(
        RecommendBroker broker, out List<string> captured, Func<Entity, string?>? recommendField = null)
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
                    RecommendField = recommendField ?? (_ => "description"),
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

        // "~" is the only trigger for the recommend side-action.
        instance.HandleInput("~");

        await ResumeWhenReady(instance);

        // Three description suggestions rendered as a numbered list.
        conn.SentText.Should().Contain(s => s.Contains("Suggestions:"));
        conn.SentText.Should().Contain(s => s.Contains("1. " + FirstDescription));

        // Picking "1" feeds the first suggestion to the step's OnInput.
        instance.HandleInput("1");

        captured.Should().ContainSingle().Which.Should().Be(FirstDescription);
    }

    [Fact]
    public void Only_tilde_triggers_the_side_action()
    {
        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));
        var (instance, session, _) = Setup(broker, out _);

        instance.Start(session);
        instance.HandleInput("~");

        // Triggering the async side-action puts the flow into awaiting-async (or it already
        // completed at delayMs:0 — either way a pending async was set).
        var fired = instance.IsAwaitingAsync || instance.TryResumeAsync();
        fired.Should().BeTrue();
    }

    // "rec"/"recommend" are no longer triggers — they are ordinary literal field values.
    [Theory]
    [InlineData("rec")]
    [InlineData("recommend")]
    public void Former_keyword_aliases_are_now_literal_values(string literal)
    {
        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));
        var (instance, session, _) = Setup(broker, out var captured);

        instance.Start(session);
        instance.HandleInput(literal);

        instance.IsAwaitingAsync.Should().BeFalse();                  // no side-action fired
        captured.Should().ContainSingle().Which.Should().Be(literal); // taken as the field value
    }

    [Fact]
    public async Task Typing_own_value_while_suggestions_pending_is_accepted_not_reprompted()
    {
        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));
        var (instance, session, conn) = Setup(broker, out var captured);

        instance.Start(session);
        instance.HandleInput("~");
        await ResumeWhenReady(instance);

        // The prompt promises "or type your own value" — a non-index value must be honored,
        // not bounced back with "Pick a number from the list".
        instance.HandleInput("A quiet stone chamber.");

        captured.Should().ContainSingle().Which.Should().Be("A quiet stone chamber.");
    }

    // Records the request the broker received, so we can assert the hint was threaded.
    private sealed class CapturingProvider : IRecommendProvider
    {
        public RecommendRequest? Last { get; private set; }
        public bool IsEnabled { get; }
        public CapturingProvider(bool isEnabled = true) { IsEnabled = isEnabled; }
        public Task<RecommendResult> RecommendAsync(RecommendRequest request)
        {
            Last = request;
            return Task.FromResult(new RecommendResult(new List<string> { "one suggestion" }));
        }
    }

    [Fact]
    public async Task Tilde_threads_trailing_text_as_the_hint()
    {
        var provider = new CapturingProvider();
        var broker = new RecommendBroker();
        broker.Register(provider);
        var (instance, session, _) = Setup(broker, out _);

        instance.Start(session);
        instance.HandleInput("~ a hallway leading to the castle gate");
        await ResumeWhenReady(instance);

        provider.Last.Should().NotBeNull();
        provider.Last!.Hint.Should().Be("a hallway leading to the castle gate");
    }

    [Fact]
    public async Task Bare_tilde_sends_a_null_hint()
    {
        var provider = new CapturingProvider();
        var broker = new RecommendBroker();
        broker.Register(provider);
        var (instance, session, _) = Setup(broker, out _);

        instance.Start(session);
        instance.HandleInput("~");
        await ResumeWhenReady(instance);

        provider.Last!.Hint.Should().BeNull();
    }

    [Fact]
    public void Tilde_when_broker_disabled_reports_unavailable_and_does_not_fire()
    {
        var broker = new RecommendBroker();
        broker.Register(new CapturingProvider(isEnabled: true));
        broker.SetEnabled(false); // admin gate off
        var (instance, session, conn) = Setup(broker, out var captured);

        instance.Start(session);
        instance.HandleInput("~");

        instance.IsAwaitingAsync.Should().BeFalse();               // no async fired
        conn.SentText.Should().Contain(s => s.Contains("unavailable"));
        captured.Should().BeEmpty();                               // "~" not consumed as a value
    }

    [Fact]
    public void Tilde_on_a_non_recommendable_field_reports_not_available_and_is_not_literal()
    {
        var broker = new RecommendBroker();
        broker.Register(new CapturingProvider(isEnabled: true));
        // This step opted into recommend, but resolves no field for the current selection
        // (mirrors edit-room.js returning null for anything but name/description).
        var (instance, session, conn) = Setup(broker, out var captured, recommendField: _ => null);

        instance.Start(session);
        instance.HandleInput("~ desert");

        instance.IsAwaitingAsync.Should().BeFalse();                        // no async fired
        conn.SentText.Should().Contain(s => s.Contains("isn't available")); // honest message
        captured.Should().BeEmpty();                                        // not set to "~ desert"
    }

    // ---- BuildRecommendContext routing (FlowEngine internal static) ----

    /// <summary>
    /// Builds the minimal plumbing needed to exercise FlowEngine.BuildRecommendContext.
    /// Approach: extracted static method, avoids the heavy FlowEngine constructor
    /// (FlowRegistry + SessionManager + PlayerCreator + ClassRegistry + ... x10).
    /// </summary>
    private static (World world, RoomProjector roomProjector, AreaRegistry areaRegistry, AreaProjector areaProjector)
        BuildProjectorFixture()
    {
        var world = new World();
        var areas = new AreaRegistry();
        var rp = new RoomProjector(world, new PropertyRegistry(), new TagRegistry(), areas);
        var ap = new AreaProjector(world, rp);
        return (world, rp, areas, ap);
    }

    [Fact]
    public void BuildRecommendContext_area_kind_with_edit_area_property_returns_AreaData()
    {
        var (world, rp, areaRegistry, areaProjector) = BuildProjectorFixture();

        var areaDef = new AreaDefinition
        {
            Id = "road-to-tar-valon",
            Name = "Road to Tar Valon",
            Short = "road",
            Description = "A long road heading east.",
            Theme = "wheel-of-time",
            Lore = "",
            LevelRange = [1, 50]
        };
        areaRegistry.Register(areaDef);

        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));

        var factory = FlowEngine.BuildRecommendContext("area", broker, world, rp, areaRegistry, areaProjector);

        factory.Should().NotBeNull("area kind with all deps present must produce a context func");

        var entity = new Entity("player", "Rand");
        entity.SetProperty("__edit_area", "road-to-tar-valon");

        var ctx = factory!(entity);

        ctx.Should().BeOfType<AreaData>();
        ((AreaData)ctx).Id.Should().Be("road-to-tar-valon");
    }

    [Fact]
    public void BuildRecommendContext_area_kind_capitalized_is_case_insensitive()
    {
        var (world, rp, areaRegistry, areaProjector) = BuildProjectorFixture();

        var areaDef = new AreaDefinition
        {
            Id = "road-to-tar-valon",
            Name = "Road to Tar Valon",
            Short = "road",
            Description = "A long road heading east.",
            Theme = "wheel-of-time",
            Lore = "",
            LevelRange = [1, 50]
        };
        areaRegistry.Register(areaDef);

        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));

        // "Area" (capitalized) must match the same as "area"
        var factory = FlowEngine.BuildRecommendContext("Area", broker, world, rp, areaRegistry, areaProjector);

        factory.Should().NotBeNull("capitalized kind must still route to area context");

        var entity = new Entity("player", "Rand");
        entity.SetProperty("__edit_area", "road-to-tar-valon");

        var ctx = factory!(entity);

        ctx.Should().BeOfType<AreaData>();
        ((AreaData)ctx).Id.Should().Be("road-to-tar-valon");
    }

    [Fact]
    public void BuildRecommendContext_room_kind_returns_RoomData()
    {
        var (world, rp, _, _) = BuildProjectorFixture();

        var room = new Room("test:hall", "The Hall", "A long hallway.");
        world.AddRoom(room);

        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));

        var factory = FlowEngine.BuildRecommendContext("room", broker, world, rp, null, null);

        factory.Should().NotBeNull();

        var entity = new Entity("player", "Rand");
        entity.LocationRoomId = "test:hall";

        var ctx = factory!(entity);

        ctx.Should().BeOfType<RoomData>();
        ((RoomData)ctx).Id.Should().Be("test:hall");
    }

    [Fact]
    public void BuildRecommendContext_null_kind_falls_back_to_room()
    {
        var (world, rp, _, _) = BuildProjectorFixture();

        var room = new Room("test:hall", "The Hall", "A long hallway.");
        world.AddRoom(room);

        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));

        var factory = FlowEngine.BuildRecommendContext(null, broker, world, rp, null, null);

        factory.Should().NotBeNull();

        var entity = new Entity("player", "Rand");
        entity.LocationRoomId = "test:hall";

        var ctx = factory!(entity);

        ctx.Should().BeOfType<RoomData>();
    }

    [Fact]
    public void BuildRecommendContext_null_broker_returns_null()
    {
        var (world, rp, areaRegistry, areaProjector) = BuildProjectorFixture();

        var factory = FlowEngine.BuildRecommendContext("area", null, world, rp, areaRegistry, areaProjector);

        factory.Should().BeNull("no broker means no recommend — context func must be null");
    }

    [Fact]
    public void BuildRecommendContext_area_kind_missing_area_def_returns_empty_AreaData()
    {
        var (world, rp, areaRegistry, areaProjector) = BuildProjectorFixture();
        // areaRegistry is empty — "unknown-area" does not exist

        var broker = new RecommendBroker();
        broker.Register(new StaticStubRecommendProvider(delayMs: 0));

        var factory = FlowEngine.BuildRecommendContext("area", broker, world, rp, areaRegistry, areaProjector);
        factory.Should().NotBeNull();

        var entity = new Entity("player", "Rand");
        entity.SetProperty("__edit_area", "unknown-area");

        var ctx = factory!(entity);

        ctx.Should().BeOfType<AreaData>("falls back to empty AreaData when def not found");
        ((AreaData)ctx).Id.Should().Be("", "empty fallback has blank Id");
    }
}
