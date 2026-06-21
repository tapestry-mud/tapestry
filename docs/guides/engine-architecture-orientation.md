# Engine Architecture Orientation: Where Things Live and How It Boots

This is a map of the Tapestry C# engine for someone about to change it. It says what each
project owns, how a server actually boots, where the pack/JavaScript line is drawn, and -
at the end - a "I want to change X, start in folder Y, read spec Z" lookup table. Read it
before your first non-trivial PR.

One rule up front, repeated at the bottom because it is the most useful thing here: the
`specs/` tree is the canonical description of current behavior. Read the spec for the area
you are touching before you read the code. The code tells you what one method does; the spec
tells you what the subsystem is *supposed* to do and why, with line-anchored citations back
into `src/`.

---

## Two halves of Tapestry

The whole project is one thesis: the engine ships plumbing, packs ship the game. In code
terms that means `src/` is plumbing only and `packs/` ship the world. There is no hardcoded
room, mob, quest, command, class, or item anywhere in `src/`. A stock engine with no packs
boots into an empty world with no commands. Everything a player ever sees comes from a pack.

The engine's side of the line is systems: an entity store, a world graph, a command router,
a game loop, a combat resolver, a registration seal, a scripting host, telnet/WebSocket
transports, persistence. The pack's side is content and behavior expressed as YAML config
plus optional JavaScript. The `CONTRIBUTING.md` coding standard states it directly:
"Mechanics go in `src/`; content goes in `packs/`." If you find yourself adding a mob name
or a command verb to a `.cs` file, you are on the wrong side of the line.

This split is what lets the ecosystem grow without engine releases. Someone publishes a
crafting system pack; a world builder depends on it and writes `craftable: true` in item
YAML; no C# changes, no engine bump. The engine's job is to make that possible, not to know
about crafting.

---

## The eight projects in Tapestry.slnx

The solution (`Tapestry.slnx`) has eight projects under `src/`. The README's Architecture
section lists only six and is stale - it omits `Tapestry.Contracts` and `Tapestry.Authoring`
and folds a couple of responsibilities together. Treat the list below as authoritative.
(Follow-up: the README's six-line architecture block should be updated to this eight-project
list.)

- **Tapestry.Shared** - the leaf everything depends on. Pure shared types with no logic:
  enums (`Direction`), the `IConnection` transport interface, `PackManifest` (the
  deserialized `pack.yaml`), `GameEvent`, `CommandContext`, `IGmcpHandler`. If a type is
  needed by two otherwise-unrelated projects, it lives here.

- **Tapestry.Contracts** - the thin interface seam between the host and the engine. Small
  on purpose: `IGameModule` (the boot-module contract the host iterates), the GMCP
  contracts (`IGmcpConnectionManager`, `IGmcpPackageHandler`, `IDirtyVitalsBatcher`). These
  let `Tapestry.Server` wire concrete implementations without the engine referencing the
  host.

- **Tapestry.Data** - server configuration. `ServerConfig` and its sections, loaded from
  `server.yaml` via YamlDotNet. This is where `tick_rate_ms`, ports, flood protection,
  persistence paths, telemetry, and the `packs:` allow-list are modeled.

- **Tapestry.Engine** - the bulk of the engine. Entities and the entity store, rooms and
  the world graph, the event bus, `CommandRouter` and the command registry, `GameLoop` /
  `GameClock`, the `Heartbeat` pulse system, and `RegistrationPolicy` (the seal). Combat,
  abilities, classes, quests, items, mobs, economy, effects, and progression are all
  subfolders here. This project knows nothing about transports or the host.

- **Tapestry.Networking** - the transports. Telnet and WebSocket servers, ANSI color
  rendering, the GMCP wire protocol, connection lifecycle. Turns bytes on a socket into
  `IConnection` instances the engine can drive.

- **Tapestry.Authoring** - the LLM "recommend" seam used by in-MUD building tools. The
  `ILlmClient` abstraction, an OpenAI-compatible client, and the prompt builders that turn a
  room or area into a model prompt. Entirely optional; off unless a server configures an
  `llm:` provider.

- **Tapestry.Scripting** - the pack/JavaScript boundary. `JintRuntime` (the embedded JS
  engine host), `PackLoader` (reads manifests, loads YAML content and scripts), the
  `Modules/` folder (the `tapestry.*` API surface exposed to pack JS), and the
  `TapestryModuleLoader` ES-module resolver. The YAML content loaders live here too.

- **Tapestry.Server** - the host. `Program.cs` is the entry point (config parse, logging,
  telemetry, the DI container, HTTP/WebSocket endpoints). `GameBootstrapper` iterates the
  `IGameModule` set. The `Modules/` folder holds the concrete boot modules
  (`ContentLoadingModule`, tick wiring, persistence, quest startup) and the GMCP package
  handlers. This is the only project that references all the others.

---

## Dependency direction

The reference arrows only ever point inward, toward `Tapestry.Shared`. The two that matter:

**Engine never references Server.** `Tapestry.Engine` cannot see the host. When the engine
needs something the host provides, it depends on an interface in `Tapestry.Contracts`
(`IGameModule`, the GMCP contracts) and the host registers the concrete type in DI. This is
why you can unit-test the engine without standing up a server.

**Pack JavaScript never references the CLR.** Pack scripts run inside Jint with no
general-purpose CLR bridge - no `AllowClr`. The only surface a pack can reach is the curated
`tapestry.*` namespace built by the `IJintApiModule` set in `Tapestry.Scripting/Modules/`.
A pack cannot `new` a C# type, touch the filesystem outside the narrow `tapestry.fs`
connections sandbox, or reach the host.

Those two lines are the same line the project draws between mechanics and content. The
engine ships the systems and the API; packs may only call the API. Neither can reach past
its boundary into the other's internals.

---

## Boot path as a sequence

`tapestry start` ultimately runs `Tapestry.Server.dll`. Here is what happens, in order
(`src/Tapestry.Server/Program.cs`):

1. **Parse launch options.** `--config <path>` and `--packs <dir>` are read via the
   command-line config provider. The config file is loaded into a `ServerConfig`, and the
   `--packs` override (if any) is stamped onto it.

2. **Logging and telemetry.** Serilog is configured to the console always, and to an
   OpenTelemetry OTLP sink when `telemetry.enabled` is true. Meter and tracer providers are
   registered only in the telemetry-on branch - the engine runs fine with telemetry off.

3. **Build the DI container.** Every engine and scripting service is registered
   (`AddTapestryEngine()`, `AddTapestryScripting()`), then persistence, GMCP infrastructure,
   the transports, and the ordered set of `IGameModule` singletons. The registration order
   of the `IGameModule`s is the boot order.

4. **Map HTTP/WebSocket endpoints.** The pre-auth credential endpoints and the WebSocket
   fallback (which carries both real player connections and anonymous watch-mode spectators)
   are mapped. None of these run game logic yet.

5. **`GameBootstrapper.Configure()`** iterates every `IGameModule` in registration order and
   calls `module.Configure()` on each. The heavy one is `ContentLoadingModule`, which drives
   pack loading (next section).

6. **Force-resolve event-subscriber singletons** so their constructors wire up their
   `EventBus` subscriptions, then configure the GMCP package handlers.

7. **`app.RunAsync()`** starts the hosted services. The first thing
   `GameLoopService.StartAsync` does is call `RegistrationPolicy.Resolve()` - the seal (next
   section but one). Only after the seal do abilities wire to commands, themes compile, help
   gap-fills, and seed players load. Then the telnet listener opens and the loop begins
   ticking.

### The two-phase pack load

Inside `ContentLoadingModule.LoadPacks` (`src/Tapestry.Server/Modules/ContentLoadingModule.cs`),
packs are loaded in two passes, and the ordering of each pass is deliberate:

- **Phase 1 - declarations**, in alphabetical discovery order. Every pack's `pack.yaml` is
  read and its tags, properties, and equipment slots are registered. After this pass,
  dependency resolvers are wired so Phase 2 YAML coercion can see properties declared by any
  pack.

- **Phase 2 - content**, in dependency-topological order. Each pack loads its rooms, items,
  mobs, scripts, help, themes, and quests only after the packs it declares a dependency on
  have already loaded. This is what makes an earlier pack's tags visible to a later pack, and
  lets a later pack's load-time JS import a dependency's exports and find them already there.

The split is the whole reason a content pack can write `terrain: urban` against a tag that a
*different* system pack declared: declarations for everyone come first, content for everyone
comes second. Independent packs (no edge between them) keep alphabetical order for
determinism. See [pack-loading.md](../../specs/pack-loading.md) for the full discovery,
filtering, manifest, and topological-sort rules.

---

## The seal

All pack registrations are declarative. When a pack calls `tapestry.commands.register(...)`,
`tapestry.theme.register(...)`, or any other `register`, nothing is written to a live
registry. The call records a `RegistrationCandidate` (kind, name, owner pack, override flag,
a deferred `Commit` lambda, and source file/line for diagnostics) into `RegistrationPolicy`.

`RegistrationPolicy.Resolve()` runs **once**, at the start of `GameLoopService.StartAsync`,
after every pack has loaded. It groups candidates by `(kind, name)`, picks an
order-independent winner per group, and replays each winner's `Commit`. Because the winner
is chosen by policy rather than by load order, two packs cannot silently last-write-wins each
other - that was the bug class the seal exists to kill.

The collision rules (`src/Tapestry.Engine/Registration/RegistrationPolicy.cs`):

- Two packs registering the same `(kind, name)` with no override is a boot error, located to
  both source files. The error names both owners and tells you to declare an override or
  rename one.
- To win a collision deliberately, the intended winner declares `override: true` on the
  registration **and** the pack declares a `dependencies:` edge on the pack it is overriding.
  Both are required - an override without the edge is a boot error.
- `kernel` and `engine` owners are not overridable. Engine-native registrations cannot be
  shadowed by a pack.
- Conflicting overrides (two packs both override the same name) and orphan overrides
  (override with no base to override) are also located boot errors.

A boot error here exits the process with `HostedServiceStartupFaulted` and a message that
names the file and reason - never a silent clobber. See
[registries-and-seal.md](../../specs/registries-and-seal.md) for the full policy, including
the sanctioned post-seal registration path used internally.

---

## The pack/JS boundary in code

`Tapestry.Scripting` is where pack JavaScript meets the engine. Three pieces:

- **`JintRuntime`** hosts a single Jint engine instance for all packs. CLR access is off
  (no `AllowClr`); the only surface packs can reach is the `tapestry.*` namespace. Each pack
  script is imported as a native ES module, so packs do not share a global scope.

- **`PackLoader`** reads each manifest and loads its YAML content and scripts. Scripts must
  declare `scripts_format: esm` (the only supported value; anything else is a located boot
  error). Each file is imported via `engine.Modules.Import` under the canonical module key
  `pack:<namespace>::<relfile>`, with `init.js` imported first if present.

- **`Modules/`** holds the `IJintApiModule` set. Each module contributes one namespace
  (`commands`, `world`, `events`, `schedule`, `combat`, ...) that becomes a named export on
  a single shared `@tapestry/engine` module. A pack imports it with
  `import * as tapestry from "@tapestry/engine"`.

The canonical, typed description of that whole surface is
[`types/tapestry-engine.d.ts`](../../types/tapestry-engine.d.ts). Keep it open while writing
or reviewing pack JS - and when you add or change a `Build()` return shape in a module, update
the `.d.ts` in the same change.

**The marshalling rule.** When you expose a CLR method to pack JS that takes a JavaScript
array, do **not** type the parameter as `JsValue[]`. Under the pinned Jint 4.x build, a CLR
`JsValue[]` parameter does not correctly receive a JS array argument. Take a single `JsValue`
parameter and unpack the array inside the method body. This is a load-bearing gotcha for
anyone adding to `Modules/`; it has its own regression test
(`tests/Tapestry.Scripting.Tests/Modules/PacksInteropModuleTests.cs`). See
[scripting-runtime.md](../../specs/scripting-runtime.md) for the module set, the ESM
resolver, and lexical pack attribution.

---

## Command dispatch in one line

`CommandRouter` routes on the first whitespace-delimited token only. `look`, `look north`,
and `look at corpse` all dispatch to the same `look` command - everything after the verb is
yours to parse. Privilege roles (`admin`, `builder`, ...) gate dispatch; the actor-type roles
`player` and `mob` describe who may invoke a command and never gate it. So
`roles: ["player", "mob"]` means "any actor", not "must hold a role". For verb-noun feel
(`edit room`, `dig north`) a pack declares one greedy `{ type: "text" }` arg and runs its own
sub-dispatch. See [command-dispatch.md](../../specs/command-dispatch.md).

---

## The game loop and tick at a glance

`GameLoop` runs single-threaded and advances on a fixed wall-clock interval -
`tick_rate_ms` in `server.yaml`, default 100 ms. One iteration is one tick. Cross-thread
work (input drained off network threads) is injected through `GameLoop.Schedule(Action)`,
the only safe cross-thread entry point, and drained at the top of each tick. `GameClock`
layers in-game time (hour, period, day) on top of the tick counter, and the `Heartbeat`
manager (`IPulseHandler` registry) drives high-frequency sub-tick work such as the combat
pulse. The pack-facing seam over all this is `tapestry.schedule` (register a per-tick
handler at boot; one global sweep over a state map beats registering handlers at runtime).
This is a pointer, not a tour - the full tick-phase model lives in
[heartbeat.md](../../specs/heartbeat.md), and in-game time semantics in
[world-simulation.md](../../specs/world-simulation.md).

---

## The test projects mirror the assemblies

There is one test project per major assembly, and the right one for your change is the one
that mirrors the project you touched:

| Test project | Covers changes in |
|--------------|-------------------|
| `tests/Tapestry.Engine.Tests` | entities, world graph, command router, registration policy, combat, the loop - anything in `Tapestry.Engine` |
| `tests/Tapestry.Networking.Tests` | telnet/WebSocket, ANSI, GMCP wire protocol |
| `tests/Tapestry.Scripting.Tests` | `PackLoader`, `JintRuntime`, the `tapestry.*` modules, ESM loading, the marshalling rule |
| `tests/Tapestry.Server.Tests` | host wiring, boot modules, end-to-end startup via `WebApplicationFactory` |

`CONTRIBUTING.md` asks that a new engine feature ship with a unit test in
`Tapestry.Engine.Tests`. Run the whole suite with `dotnet test`. Note that pack JavaScript
itself is not unit-tested in this tree - the gate for pack behavior is engine strict boot
plus playtest, not Node tests.

---

## "I want to change X, start in folder Y, read spec Z"

Read the spec first - it is the canonical statement of current behavior, with line anchors
back into the code. Then open the folder.

| I want to change... | Start in (`src/`) | Read spec first |
|---------------------|-------------------|-----------------|
| how packs are discovered, filtered, or load-ordered | `Tapestry.Server/Modules/ContentLoadingModule.cs`, `Tapestry.Scripting/PackLoader.cs` | [pack-loading.md](../../specs/pack-loading.md) |
| how registration collisions / overrides resolve | `Tapestry.Engine/Registration/RegistrationPolicy.cs` | [registries-and-seal.md](../../specs/registries-and-seal.md) |
| the `tapestry.*` JS API or a new module | `Tapestry.Scripting/Modules/`, `types/tapestry-engine.d.ts` | [scripting-runtime.md](../../specs/scripting-runtime.md) |
| how commands route, gate, or parse args | `Tapestry.Engine/CommandRouter.cs`, `Tapestry.Engine/ArgResolver.cs` | [command-dispatch.md](../../specs/command-dispatch.md) |
| the game loop, ticks, or scheduling | `Tapestry.Engine/GameLoop.cs`, `Tapestry.Engine/Heartbeat/` | [heartbeat.md](../../specs/heartbeat.md) |
| in-game time / day-night | `Tapestry.Engine/GameClock.cs` | [world-simulation.md](../../specs/world-simulation.md) |
| combat resolution | `Tapestry.Engine/Combat/` | [combat-resolution.md](../../specs/combat-resolution.md) |
| telnet/WebSocket, ANSI, GMCP wire | `Tapestry.Networking/` | [telnet-protocol.md](../../specs/telnet-protocol.md), [gmcp.md](../../specs/gmcp.md) |
| server config schema | `Tapestry.Data/ServerConfig.cs` | the relevant subsystem spec |
| host startup / DI wiring / boot modules | `Tapestry.Server/Program.cs`, `Tapestry.Server/Modules/` | [pack-loading.md](../../specs/pack-loading.md) for the load step |
| the LLM building seam | `Tapestry.Authoring/` | [area-authoring.md](../../specs/area-authoring.md) |
| persistence / saves / accounts | `Tapestry.Engine/Persistence/`, `Tapestry.Server/Persistence/` | [persistence.md](../../specs/persistence.md), [login-and-accounts.md](../../specs/login-and-accounts.md) |

If you want to *try* a change against a running server without touching engine code, the
companion guide [prototyping-with-a-throwaway-pack.md](./prototyping-with-a-throwaway-pack.md)
shows how to boot a local engine build over a disposable pack and connect over telnet.

The rule, one more time: the spec is the canonical current behavior. Read it before the code,
and when your change lands, update the spec in the same PR.
