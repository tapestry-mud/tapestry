using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Engine.Cutscene;
using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests.Modules;

public class CutsceneModuleTests
{
    private (JintRuntime rt, World world, SessionManager sessions, CutsceneManager cutscenes) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (rt, provider.GetRequiredService<World>(), provider.GetRequiredService<SessionManager>(),
            provider.GetRequiredService<CutsceneManager>());
    }

    private (PlayerSession Session, FakeConnection Conn) AddOnlinePlayer(SessionManager sessions, World world, string name)
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", name);
        world.TrackEntity(entity);
        var session = new PlayerSession(conn, entity) { Phase = LoginPhase.Playing };
        sessions.Add(session);
        return (session, conn);
    }

    [Fact]
    public void Play_ParsesBeatsArray_AndEmitsTheFirstBeat()
    {
        var (rt, world, sessions, _) = BuildRuntime();
        var (session, conn) = AddOnlinePlayer(sessions, world, "Perrin");

        EsmTest.Load(rt, "test-pack", $@"
            tapestry.cutscene.play('{session.PlayerEntity.Id}', [
                {{ text: 'The Weaver bends over the loom.', pauseAfter: 5 }},
                {{ text: 'A flash.', pauseAfter: 0 }}
            ], {{ skippable: true }});
        ");

        conn.SentText.Should().Contain(l => l.Contains("The Weaver bends over the loom."));
        conn.SentText.Should().NotContain(l => l.Contains("A flash."));
        session.IsPromptHeld.Should().BeTrue();
    }

    [Fact]
    public void Play_MissingPauseAfter_FallsBackToEngineDefault()
    {
        var (rt, world, sessions, cutscenes) = BuildRuntime();
        var (session, _) = AddOnlinePlayer(sessions, world, "Faile");

        EsmTest.Load(rt, "test-pack", $@"
            tapestry.cutscene.play('{session.PlayerEntity.Id}', [
                {{ text: 'beat0' }},
                {{ text: 'beat1' }}
            ]);
        ");

        cutscenes.IsActive(session.PlayerEntity.Id).Should().BeTrue();
        // Beat 0 was emitted with the default pause; it should NOT yet have advanced to beat1
        // one tick later (default is far larger than 1).
        cutscenes.AdvanceAll(1);
        ((FakeConnection)session.Connection).SentText.Should().NotContain(l => l.Contains("beat1"));
    }

    [Fact]
    public void Play_InvokesOnComplete_WhenTheSequenceFinishes()
    {
        var (rt, world, sessions, _) = BuildRuntime();
        var (session, _) = AddOnlinePlayer(sessions, world, "Loial");

        EsmTest.Load(rt, "test-pack", $@"
            tapestry.world.setProperty('{session.PlayerEntity.Id}', 'opener_done', false);
            tapestry.cutscene.play('{session.PlayerEntity.Id}', [
                {{ text: 'only beat', pauseAfter: 0 }}
            ], {{ skippable: true }}, function() {{
                tapestry.world.setProperty('{session.PlayerEntity.Id}', 'opener_done', true);
            }});
        ");

        session.PlayerEntity.GetProperty<bool>("opener_done").Should().BeTrue();
        session.IsPromptHeld.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ReflectsCutsceneManagerState()
    {
        var (rt, world, sessions, cutscenes) = BuildRuntime();
        var (session, _) = AddOnlinePlayer(sessions, world, "Min");

        EsmTest.Load(rt, "test-pack", $@"
            tapestry.cutscene.play('{session.PlayerEntity.Id}', [
                {{ text: 'beat0', pauseAfter: 100 }},
                {{ text: 'beat1', pauseAfter: 0 }}
            ], {{ skippable: true }});
        ");

        var activeDuring = EsmTest.Eval(rt, $"tapestry.cutscene.isActive('{session.PlayerEntity.Id}')");
        Convert.ToBoolean(activeDuring).Should().BeTrue();

        cutscenes.IsActive(session.PlayerEntity.Id).Should().BeTrue();
    }
}
