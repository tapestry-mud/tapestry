using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Tags;
using Tapestry.Engine.Ui;
using Tapestry.Scripting.Services;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

internal record GrantKindRegistration(
    string Kind,
    string Type,
    IReadOnlyList<string> AppliesTo,
    string Help,
    string Pack,
    JsValue Handler);

public class AdminModule : IJintApiModule
{
    private readonly World _world;
    private readonly ApiMessaging _messaging;
    private readonly SessionManager _sessions;
    private readonly PanelRenderer _renderer;
    private readonly ILogger<AdminModule> _logger;
    private readonly PropertyRegistry _propertyRegistry;
    private readonly TagRegistry _tagRegistry;
    private readonly AttributeWriter _attributeWriter;

    private readonly Dictionary<(string Kind, string Type), GrantKindRegistration> _grantKinds = new();

    private static readonly HashSet<string> AllowedKinds =
        new(StringComparer.OrdinalIgnoreCase) { "player", "npc", "item", "room" };

    public AdminModule(
        World world,
        ApiMessaging messaging,
        SessionManager sessions,
        PanelRenderer renderer,
        ILogger<AdminModule> logger,
        PropertyRegistry propertyRegistry,
        TagRegistry tagRegistry)
    {
        _world = world;
        _messaging = messaging;
        _sessions = sessions;
        _renderer = renderer;
        _logger = logger;
        _propertyRegistry = propertyRegistry;
        _tagRegistry = tagRegistry;
        _attributeWriter = new AttributeWriter(propertyRegistry, tagRegistry);
    }

    public string Namespace => "admin";

    public object Build(JintEngine engine)
    {
        return new
        {
            set = new
            {
                dispatch = new Action<string, JsValue>((adminIdStr, argsVal) =>
                {
                    DispatchSet(adminIdStr, ConvertJsArgs(argsVal));
                })
            },
            grant = new
            {
                register = new Action<JsValue>(def => RegisterGrantKind(engine, def)),
                dispatch = new Action<string, JsValue>((adminIdStr, argsVal) =>
                {
                    DispatchGrant(engine, adminIdStr, ConvertJsArgs(argsVal));
                }),
                listKinds = new Func<object[]>(ListGrantKinds),
                getKind = new Func<string, string, object?>(GetGrantKind)
            },
            resolveTarget = new Func<string, string, string, object?>((adminIdStr, keyword, targetKind) =>
            {
                if (!Guid.TryParse(adminIdStr, out var adminId)) { return ErrorResult("not_found", "Invalid admin ID."); }
                var result = ResolveTarget(adminId, keyword, targetKind.ToLower());
                if (!result.Ok) { return ErrorResult(result.Error, result.Message); }
                return new { ok = true, id = result.Id.ToString(), name = result.Name, entity_kind = targetKind };
            }),
            setEntityHp = new Action<string, int>((entityIdStr, value) =>
            {
                if (!Guid.TryParse(entityIdStr, out var id)) { return; }
                var entity = _world.GetEntity(id);
                if (entity == null) { return; }
                entity.Stats.BaseMaxHp = value;
                entity.Stats.Invalidate();
                entity.Stats.Hp = entity.Stats.MaxHp;
            }),
        };
    }

    private object[] ListGrantKinds()
    {
        return _grantKinds.Values
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.Type)
            .Select(r => (object)new
            {
                kind = r.Kind,
                type = r.Type,
                applies_to = r.AppliesTo.ToArray(),
                help = r.Help,
                pack = r.Pack
            })
            .ToArray();
    }

    private object? GetGrantKind(string kind, string type)
    {
        if (!_grantKinds.TryGetValue((kind.ToLower(), type.ToLower()), out var reg)) { return null; }
        return new { kind = reg.Kind, type = reg.Type, applies_to = reg.AppliesTo.ToArray(), help = reg.Help, pack = reg.Pack };
    }

    private void RegisterGrantKind(JintEngine engine, JsValue def)
    {
        if (def is not ObjectInstance obj) { return; }
        var kind = obj.Get("kind").ToString().ToLower();
        var type = obj.Get("type").ToString().ToLower();

        if (!AllowedKinds.Contains(kind))
        {
            _logger.LogWarning("AdminModule: ignoring grant.register for unknown kind '{Kind}'", kind);
            return;
        }

        var appliesTo = ParseStringArray(obj.Get("applies_to"));
        if (appliesTo.Count == 0) { appliesTo = ["*"]; }

        var helpVal = obj.Get("help");
        var help = helpVal.Type != Types.Undefined ? helpVal.ToString() : "";
        var handler = obj.Get("handler");

        var packNameVal = engine.GetValue("__currentPack");
        var pack = packNameVal.Type != Types.Undefined ? packNameVal.ToString() : "";

        var key = (kind, type);
        if (_grantKinds.ContainsKey(key))
        {
            _logger.LogWarning("AdminModule: duplicate grant kind ({Kind}, {Type}) - later registration wins", kind, type);
        }
        _grantKinds[key] = new GrantKindRegistration(kind, type, appliesTo, help, pack, handler);
    }

    private void DispatchSet(string adminIdStr, string[] args)
    {
        if (!Guid.TryParse(adminIdStr, out var adminId)) { return; }

        if (args.Length == 0 || args[0] == "?")
        {
            SendKindsPanel(adminId, "set");
            return;
        }

        var kind = args[0].ToLower();
        if (!AllowedKinds.Contains(kind))
        {
            _messaging.Send(adminId, $"Unknown kind: {args[0]}. Try `set ?`.\r\n");
            return;
        }

        if (args.Length == 1 || args[1] == "?")
        {
            SendKindAttributesPanel(adminId, kind);
            return;
        }

        var attr = args[1].ToLower();

        if (args.Length >= 3 && args[2] == "?")
        {
            if (IsDeclaredForKind(attr, kind))
            {
                SendAttributeUsagePanel(adminId, kind, attr);
                return;
            }
            var qTarget = ResolveTarget(adminId, args[1], kind);
            if (qTarget.Ok)
            {
                SendTargetAttributesPanel(adminId, kind, qTarget);
                return;
            }
            _messaging.Send(adminId, qTarget.Message + "\r\n");
            return;
        }

        if (kind == "room")
        {
            // Rooms aren't name-resolvable: the target is always the admin's current room,
            // so there is no target token -- value tokens start at args[2].
            var roomRes = ResolveTarget(adminId, "", kind);
            if (!roomRes.Ok)
            {
                _messaging.Send(adminId, roomRes.Message + "\r\n");
                return;
            }

            var room = _world.GetRoom(roomRes.RoomId ?? "");
            if (room == null)
            {
                _messaging.Send(adminId, "Room not found.\r\n");
                return;
            }

            var roomRest = args.Length > 2 ? args[2..] : Array.Empty<string>();
            var roomResult = roomRest.Length == 0
                ? _attributeWriter.Describe(room, attr)
                : _attributeWriter.Write(room, attr, roomRest);

            _messaging.Send(adminId, roomResult.Message + "\r\n");
            return;
        }

        if (args.Length < 3)
        {
            _messaging.Send(adminId, $"Usage: set {kind} {attr} <target> <value>\r\n");
            return;
        }

        var targetRes = ResolveTarget(adminId, args[2], kind);
        if (!targetRes.Ok)
        {
            _messaging.Send(adminId, targetRes.Message + "\r\n");
            return;
        }

        var entity = _world.GetEntity(targetRes.Id);
        if (entity == null)
        {
            _messaging.Send(adminId, $"Could not resolve {targetRes.Name}.\r\n");
            return;
        }

        var rest = args.Length > 3 ? args[3..] : Array.Empty<string>();

        var result = rest.Length == 0
            ? _attributeWriter.Describe(entity, attr)
            : _attributeWriter.Write(entity, attr, rest);

        _messaging.Send(adminId, result.Message + "\r\n");
    }

    private void DispatchGrant(JintEngine engine, string adminIdStr, string[] args)
    {
        if (!Guid.TryParse(adminIdStr, out var adminId)) { return; }

        if (args.Length == 0 || args[0] == "?")
        {
            SendKindsPanel(adminId, "grant");
            return;
        }

        var kind = args[0].ToLower();
        if (!AllowedKinds.Contains(kind))
        {
            _messaging.Send(adminId, $"Unknown kind: {args[0]}. Try `grant ?`.\r\n");
            return;
        }

        if (args.Length == 1 || args[1] == "?")
        {
            SendKindTypesPanel(adminId, "grant", kind);
            return;
        }

        var arg2 = args[1].ToLower();

        if (args.Length >= 3 && args[2] == "?")
        {
            if (_grantKinds.TryGetValue((kind, arg2), out var kindReg))
            {
                SendTypeUsagePanel(adminId, "grant", kind, arg2, kindReg.Help, kindReg.AppliesTo);
                return;
            }
            _messaging.Send(adminId, $"Unknown {kind} grant: {args[1]}. Try `grant {kind} ?`.\r\n");
            return;
        }

        if (!_grantKinds.TryGetValue((kind, arg2), out var reg))
        {
            _messaging.Send(adminId, $"Unknown {kind} grant: {args[1]}. Try `grant {kind} ?`.\r\n");
            return;
        }

        if (args.Length < 3)
        {
            _messaging.Send(adminId, $"Usage: {reg.Help}\r\n");
            return;
        }

        var targetRes = ResolveTarget(adminId, args[2], kind);
        if (!targetRes.Ok)
        {
            _messaging.Send(adminId, targetRes.Message + "\r\n");
            return;
        }

        var rest = args.Length > 3 ? args[3..] : Array.Empty<string>();
        var adminObj = BuildAdminObj(adminId);
        var targetObj = new { id = targetRes.Id.ToString(), name = targetRes.Name, entity_kind = kind };
        engine.InvokeAsPack(reg.Pack, reg.Handler, null, new object[] { adminObj, targetObj, rest });
    }

    private void SendKindsPanel(Guid adminId, string verb)
    {
        var rows = new List<Row>
        {
            new CellRow { Cells = [
                new Cell { Content = "  player", Width = CellWidth.Fixed(14) },
                new Cell { Content = "Modify a player character.", Width = CellWidth.Fill }
            ]},
            new CellRow { Cells = [
                new Cell { Content = "  npc", Width = CellWidth.Fixed(14) },
                new Cell { Content = "Modify a mob instance in your room.", Width = CellWidth.Fill }
            ]},
            new CellRow { Cells = [
                new Cell { Content = "  item", Width = CellWidth.Fixed(14) },
                new Cell { Content = "Modify an item you are holding.", Width = CellWidth.Fill }
            ]}
        };
        var footer = $"{verb} [kind] ? - see types   |   {verb} [kind] [target] ? - target-specific types";
        SendPanel(adminId, $"Admin: {verb}", rows, footer);
    }

    private void SendKindTypesPanel(Guid adminId, string verb, string kind)
    {
        var registry = _grantKinds.Values.Where(r => r.Kind == kind).Select(r => (r.Type, r.AppliesTo, r.Help));

        var rows = new List<Row>();
        foreach (var t in registry.OrderBy(r => r.Type))
        {
            var appliesToSummary = t.AppliesTo.Contains("*") ? "any" : string.Join("/", t.AppliesTo);
            var firstSentence = t.Help.Split(new[] { '.', '-' }, 2).First().Trim();
            rows.Add(new CellRow { Cells = [
                new Cell { Content = "  " + t.Type, Width = CellWidth.Fixed(16) },
                new Cell { Content = appliesToSummary, Width = CellWidth.Fixed(10) },
                new Cell { Content = firstSentence, Width = CellWidth.Fill }
            ]});
        }
        if (rows.Count == 0)
        {
            rows.Add(new TextRow { Content = "  No types registered for this kind yet." });
        }
        var footer = $"{verb} {kind} [type] [target] [value]   |   {verb} {kind} [type] ? - usage";
        SendPanel(adminId, $"Admin: {verb} {kind}", rows, footer);
    }

    private void SendTypeUsagePanel(Guid adminId, string verb, string kind, string type, string help, IReadOnlyList<string> appliesTo)
    {
        var appliesToStr = appliesTo.Contains("*") ? "any" : string.Join(", ", appliesTo);
        var rows = new List<Row>
        {
            new CellRow { Cells = [
                new Cell { Content = "  Usage:", Width = CellWidth.Fixed(12) },
                new Cell { Content = help, Width = CellWidth.Fill }
            ]},
            new CellRow { Cells = [
                new Cell { Content = "  Applies:", Width = CellWidth.Fixed(12) },
                new Cell { Content = appliesToStr, Width = CellWidth.Fill }
            ]}
        };
        SendPanel(adminId, $"{verb} {kind} {type}", rows, footer: null);
    }

    private IEnumerable<(string Name, string ValueType, string Help, bool IsTag)> AttributesForKind(string kind)
    {
        foreach (var p in _propertyRegistry.GetAll())
        {
            if (p.AppliesToType(kind) && p.IsAdminSettable && !p.IsCollectionType)
            {
                yield return (p.Name, AttributeWriter.ValueTypeName(p.ValueType), p.Description, false);
            }
        }
        foreach (var t in _tagRegistry.GetAll())
        {
            if (t.AppliesToType(kind))
            {
                yield return (t.Name, "tag", t.Description, true);
            }
        }
    }

    private bool IsDeclaredForKind(string attr, string kind) =>
        AttributesForKind(kind).Any(a => a.Name.Equals(attr, StringComparison.OrdinalIgnoreCase));

    private void SendKindAttributesPanel(Guid adminId, string kind)
    {
        var rows = new List<Row>();
        foreach (var a in AttributesForKind(kind).OrderBy(a => a.Name))
        {
            rows.Add(new CellRow { Cells = [
                new Cell { Content = "  " + a.Name, Width = CellWidth.Fixed(24) },
                new Cell { Content = a.ValueType, Width = CellWidth.Fixed(12) },
                new Cell { Content = a.Help, Width = CellWidth.Fill }
            ]});
        }
        if (rows.Count == 0)
        {
            rows.Add(new TextRow { Content = "  No settable attributes for this kind yet." });
        }
        var footer = $"set {kind} [attr] [target] [value]   |   set {kind} [attr] ? - usage";
        SendPanel(adminId, $"Admin: set {kind}", rows, footer);
    }

    private void SendAttributeUsagePanel(Guid adminId, string kind, string attr)
    {
        var info = AttributesForKind(kind).First(a => a.Name.Equals(attr, StringComparison.OrdinalIgnoreCase));
        var hint = info.IsTag ? "<on|off>" : (info.ValueType == "bool" ? "<true|false>" : "<value>");
        var rows = new List<Row>
        {
            new CellRow { Cells = [
                new Cell { Content = "  Usage:", Width = CellWidth.Fixed(12) },
                new Cell { Content = $"set {kind} {attr} <target> {hint}", Width = CellWidth.Fill }
            ]},
            new CellRow { Cells = [
                new Cell { Content = "  Type:", Width = CellWidth.Fixed(12) },
                new Cell { Content = info.ValueType, Width = CellWidth.Fill }
            ]}
        };
        SendPanel(adminId, $"set {kind} {attr}", rows, footer: null);
    }

    private void SendTargetAttributesPanel(Guid adminId, string kind, TargetResult target)
    {
        var rows = new List<Row>();
        foreach (var a in AttributesForKind(kind).OrderBy(a => a.Name))
        {
            rows.Add(new CellRow { Cells = [
                new Cell { Content = "  " + a.Name, Width = CellWidth.Fixed(24) },
                new Cell { Content = a.Help, Width = CellWidth.Fill }
            ]});
        }
        if (rows.Count == 0)
        {
            rows.Add(new TextRow { Content = "  No applicable attributes for this target." });
        }
        var footer = $"set {kind} [attr] {target.Name} [value]";
        SendPanel(adminId, $"Editing: {target.Name}", rows, footer);
    }

    private void SendPanel(Guid adminId, string title, List<Row> bodyRows, string? footer)
    {
        var sections = new List<Section>
        {
            new() { Rows = [new TitleRow { Left = title, Right = "" }], SeparatorAbove = RuleStyle.None },
            new() { Rows = bodyRows, SeparatorAbove = RuleStyle.Minor }
        };
        if (footer != null)
        {
            sections.Add(new Section
            {
                Rows = [new FooterRow { Content = footer }],
                SeparatorAbove = RuleStyle.Major
            });
        }
        var panel = new Panel { Sections = sections };
        _messaging.Send(adminId, "\r\n" + _renderer.Render(panel) + "\r\n");
    }

    private object BuildAdminObj(Guid adminId)
    {
        var adminEntity = _world.GetEntity(adminId);
        var roomId = adminEntity?.LocationRoomId ?? "";
        var name = adminEntity?.Name ?? "";
        return new
        {
            entityId = adminId.ToString(),
            name,
            roomId,
            hasRole = new Func<string, bool>(role => adminEntity?.HasRole(role) ?? false),
            hasTag = new Func<string, bool>(tag => adminEntity?.HasTag(tag) ?? false),
            send = new Action<string>(text => { _messaging.Send(adminId, text); }),
            sendToRoom = new Action<string>(text =>
            {
                if (!string.IsNullOrEmpty(roomId))
                {
                    _messaging.SendToRoomExcept(roomId, adminId.ToString(), text);
                }
            })
        };
    }

    private record TargetResult(bool Ok, Guid Id, string Name, string Error, string Message, string? RoomId = null)
    {
        public static TargetResult Success(Guid id, string name) =>
            new(true, id, name, "", "");
        public static TargetResult SuccessRoom(string roomId) =>
            new(true, Guid.Empty, roomId, "", "", roomId);
        public static TargetResult Failure(string error, string message) =>
            new(false, Guid.Empty, "", error, message);
    }

    private TargetResult ResolveTarget(Guid adminId, string keyword, string kind)
    {
        if (kind == "room")
        {
            var adminEntity = _world.GetEntity(adminId);
            if (string.IsNullOrEmpty(adminEntity?.LocationRoomId))
            {
                return TargetResult.Failure("no_room", "You are not in a room.");
            }
            return TargetResult.SuccessRoom(adminEntity.LocationRoomId);
        }

        var ordinal = 1;
        var match = System.Text.RegularExpressions.Regex.Match(keyword, @"^(\d+)\.(.+)$");
        if (match.Success)
        {
            ordinal = int.Parse(match.Groups[1].Value);
            keyword = match.Groups[2].Value;
        }
        var kwLower = keyword.ToLower();

        if (kind == "npc")
        {
            var adminEntity = _world.GetEntity(adminId);
            if (string.IsNullOrEmpty(adminEntity?.LocationRoomId))
            {
                return TargetResult.Failure("no_room", "You are not anywhere.");
            }
        }

        var candidates = kind switch
        {
            "player" => CollectPlayerCandidates(adminId, kwLower),
            "npc" => CollectNpcCandidates(adminId, kwLower),
            "item" => CollectItemCandidates(adminId, kwLower),
            _ => new List<(Guid Id, string Name)>()
        };

        if (candidates.Count == 0)
        {
            return kind switch
            {
                "player" => TargetResult.Failure("not_found", $"No player matches '{keyword}'."),
                "npc" => TargetResult.Failure("not_in_room", $"No mob named '{keyword}' here."),
                "item" => TargetResult.Failure("not_held", $"You are not holding '{keyword}'."),
                _ => TargetResult.Failure("not_found", $"No target matches '{keyword}'.")
            };
        }

        if (ordinal > candidates.Count)
        {
            return TargetResult.Failure("not_found", $"No {OrdinalSuffix(ordinal)} '{keyword}' matches.");
        }

        var chosen = candidates[ordinal - 1];
        return TargetResult.Success(chosen.Id, chosen.Name);
    }

    private List<(Guid Id, string Name)> CollectPlayerCandidates(Guid adminId, string kwLower)
    {
        var adminEntity = _world.GetEntity(adminId);
        if (kwLower is "self" or "me" || kwLower == (adminEntity?.Name.ToLower() ?? "\0"))
        {
            return adminEntity != null
                ? [(adminId, adminEntity.Name)]
                : [];
        }

        var results = new List<(Guid Id, string Name)>();
        var seen = new HashSet<Guid>();
        var onlineSessions = _sessions.AllSessions.ToList();

        foreach (var session in onlineSessions)
        {
            if (session.PlayerEntity.Name.ToLower() == kwLower && seen.Add(session.PlayerEntity.Id))
            {
                results.Add((session.PlayerEntity.Id, session.PlayerEntity.Name));
            }
        }

        var adminRoomId = adminEntity?.LocationRoomId;
        if (adminRoomId != null)
        {
            foreach (var session in onlineSessions)
            {
                if (session.PlayerEntity.LocationRoomId == adminRoomId &&
                    session.PlayerEntity.Name.ToLower().StartsWith(kwLower) &&
                    seen.Add(session.PlayerEntity.Id))
                {
                    results.Add((session.PlayerEntity.Id, session.PlayerEntity.Name));
                }
            }
        }

        foreach (var session in onlineSessions)
        {
            if (session.PlayerEntity.Name.ToLower().StartsWith(kwLower) &&
                seen.Add(session.PlayerEntity.Id))
            {
                results.Add((session.PlayerEntity.Id, session.PlayerEntity.Name));
            }
        }

        return results;
    }

    private List<(Guid Id, string Name)> CollectNpcCandidates(Guid adminId, string kwLower)
    {
        var adminEntity = _world.GetEntity(adminId);
        var roomId = adminEntity?.LocationRoomId;
        if (roomId == null) { return []; }

        var room = _world.GetRoom(roomId);
        if (room == null) { return []; }

        var results = new List<(Guid Id, string Name)>();
        foreach (var entity in room.Entities)
        {
            if (entity.Type != EntityTypes.Npc) { continue; }
            if (MatchesName(entity.Name, kwLower))
            {
                results.Add((entity.Id, entity.Name));
                continue;
            }
            var keywords = entity.GetProperty<List<string>>("keywords");
            if (keywords?.Any(k => k.ToLower().StartsWith(kwLower)) ?? false)
            {
                results.Add((entity.Id, entity.Name));
            }
        }
        return results;
    }

    private List<(Guid Id, string Name)> CollectItemCandidates(Guid adminId, string kwLower)
    {
        var adminEntity = _world.GetEntity(adminId);
        if (adminEntity == null) { return []; }

        var results = new List<(Guid Id, string Name)>();
        foreach (var item in adminEntity.Contents)
        {
            if (MatchesItem(item, kwLower)) { results.Add((item.Id, item.Name)); }
        }
        foreach (var item in adminEntity.Equipment.Values)
        {
            if (MatchesItem(item, kwLower)) { results.Add((item.Id, item.Name)); }
        }
        return results;
    }

    private static bool MatchesItem(Entity item, string kwLower) =>
        MatchesName(item.Name, kwLower) ||
        item.Tags.Any(t => t.ToLower().StartsWith(kwLower));

    private static bool MatchesName(string name, string kwLower)
    {
        var lower = name.ToLower();
        if (lower.StartsWith(kwLower)) { return true; }
        var wordStart = lower.IndexOf(' ');
        while (wordStart >= 0 && wordStart < lower.Length - 1)
        {
            if (lower.AsSpan(wordStart + 1).StartsWith(kwLower)) { return true; }
            wordStart = lower.IndexOf(' ', wordStart + 1);
        }
        return false;
    }

    private static List<string> ParseStringArray(JsValue val)
    {
        var list = new List<string>();
        if (val is not JsArray arr) { return list; }
        for (uint i = 0; i < arr.Length; i++)
        {
            var el = arr[(int)i];
            if (el.Type != Types.Undefined && el.Type != Types.Null)
            {
                list.Add(el.ToString());
            }
        }
        return list;
    }

    private static string[] ConvertJsArgs(JsValue val)
    {
        if (val is JsArray arr)
        {
            var result = new string[(int)arr.Length];
            for (uint i = 0; i < arr.Length; i++)
            {
                var item = arr[(int)i];
                result[i] = (item.Type == Types.Undefined || item.Type == Types.Null) ? "" : item.ToString();
            }
            return result;
        }
        return [];
    }

    private static object ErrorResult(string error, string message) => new { ok = false, error, message };

    private static string OrdinalSuffix(int n) => (n % 100) switch
    {
        11 or 12 or 13 => $"{n}th",
        _ => (n % 10) switch
        {
            1 => $"{n}st",
            2 => $"{n}nd",
            3 => $"{n}rd",
            _ => $"{n}th"
        }
    };
}
