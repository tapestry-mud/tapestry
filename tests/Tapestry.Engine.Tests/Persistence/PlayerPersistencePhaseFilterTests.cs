using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Persistence;

public class PlayerPersistencePhaseFilterTests
{
    private readonly FakePlayerStore _store;
    private readonly SessionManager _sessions;
    private readonly PlayerPersistenceService _svc;

    public PlayerPersistencePhaseFilterTests()
    {
        _store = new FakePlayerStore();
        var registry = new PropertyTypeRegistry();
        CommonProperties.Register(registry);
        var serializer = new PlayerSerializer(registry);
        _sessions = new SessionManager();
        var world = new World();
        _svc = new PlayerPersistenceService(
            _store, serializer, _sessions, world,
            NullLogger<PlayerPersistenceService>.Instance);
    }

    private PlayerSession MakeSession(string name, LoginPhase phase)
    {
        var conn = new FakeConnection();
        var entity = new Entity("player", name);
        entity.LocationRoomId = "limbo:recall";
        var session = new PlayerSession(conn, entity);
        session.Phase = phase;
        return session;
    }

    [Fact]
    public async Task SaveAllPlayers_SkipsCreatingSessions()
    {
        var creatingSession = MakeSession("Newbie", LoginPhase.Creating);
        _sessions.Add(creatingSession);

        await _svc.SaveAllPlayers();

        _store.SavedNames.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAllPlayers_SavesPlayingSessions()
    {
        var playingSession = MakeSession("Veteran", LoginPhase.Playing);
        _sessions.Add(playingSession);
        _svc.TrackPasswordHash(playingSession.PlayerEntity.Id, "$2a$12$fakehash");

        await _svc.SaveAllPlayers();

        _store.SavedNames.Should().Contain("Veteran");
    }

    [Fact]
    public async Task SaveAllPlayers_MixedPhases_OnlySavesPlaying()
    {
        var creating = MakeSession("Ghost", LoginPhase.Creating);
        var playing = MakeSession("Hero", LoginPhase.Playing);
        _sessions.Add(creating);
        _sessions.Add(playing);
        _svc.TrackPasswordHash(playing.PlayerEntity.Id, "$2a$12$fakehash");

        await _svc.SaveAllPlayers();

        _store.SavedNames.Should().ContainSingle().Which.Should().Be("Hero");
    }
}

// Minimal fake — only records names saved, never touches disk
internal class FakePlayerStore : IPlayerStore
{
    public List<string> SavedNames { get; } = new();

    public Task SaveAsync(PlayerSaveData data)
    {
        SavedNames.Add(data.Name);
        return Task.CompletedTask;
    }

    public Task<PlayerSaveData?> LoadAsync(string playerName)
    {
        return Task.FromResult<PlayerSaveData?>(null);
    }

    public bool Exists(string playerName)
    {
        return false;
    }

    public Task DeleteAsync(string playerName)
    {
        return Task.CompletedTask;
    }
}
