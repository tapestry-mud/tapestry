using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Sustenance;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class CharStatusHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly DirtyVitalsBatcher _batcher;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly ProgressionManager _progression;
    private readonly AlignmentManager _alignment;
    private readonly SustenanceConfig _sustenance;

    public string Name => "CharStatus";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "Char.Status", "Char.StatusVars" };

    public CharStatusHandler(
        IGmcpConnectionManager connectionManager,
        DirtyVitalsBatcher batcher,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        ProgressionManager progression,
        AlignmentManager alignment,
        SustenanceConfig sustenance)
    {
        _connectionManager = connectionManager;
        _batcher = batcher;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _progression = progression;
        _alignment = alignment;
        _sustenance = sustenance;
    }

    public void Configure()
    {
        _eventBus.Subscribe("progression.level.up", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var entity = _world.GetEntity(evt.SourceEntityId.Value);
            if (entity == null) { return; }
            SendCharStatus(evt.SourceEntityId.Value, entity);
            _batcher.MarkDirty(evt.SourceEntityId.Value);
        });
    }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        _connectionManager.Send(connectionId, "Char.StatusVars", new
        {
            hp = "Current HP",
            maxhp = "Max HP",
            mana = "Current Mana",
            maxmana = "Max Mana",
            mv = "Current Movement",
            maxmv = "Max Movement",
            name = "Character name",
            race = "Race",
            @class = "Class",
            level = "Level",
        });
        _connectionManager.Send(connectionId, "Char.Status", BuildStatusPayload(e));
    }

    private void SendCharStatus(Guid entityId, Entity entity)
    {
        _connectionManager.Send(entityId, "Char.Status", BuildStatusPayload(entity));
    }

    private object BuildStatusPayload(Entity entity)
    {
        var alignment = _alignment.Get(entity.Id);
        var alignmentBucket = _alignment.Bucket(entity.Id);
        var gold = entity.GetProperty<int>(CurrencyProperties.Gold);
        var hungerValue = entity.TryGetProperty<int>(SustenanceProperties.Sustenance, out var sv) ? sv : 100;
        var hungerTier = _sustenance.GetTier(hungerValue);

        return new
        {
            name = entity.Name,
            race = entity.GetProperty<string?>(CommonProperties.Race) ?? "",
            @class = entity.GetProperty<string?>(CommonProperties.Class) ?? "",
            level = _progression.GetAllTracks()
                .Select(t => _progression.GetLevel(entity.Id, t.Name))
                .DefaultIfEmpty(0)
                .Max(),
            str = entity.Stats.Strength,
            @int = entity.Stats.Intelligence,
            wis = entity.Stats.Wisdom,
            dex = entity.Stats.Dexterity,
            con = entity.Stats.Constitution,
            luk = entity.Stats.Luck,
            alignment,
            alignmentBucket,
            gold,
            hungerTier,
            hungerValue,
            isAdmin = entity.HasTag("admin"),
        };
    }
}
