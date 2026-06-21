# Your First Engine Change: Edit, Test, Verify

Tapestry is a C# MUD engine. The game *content* lives in JavaScript packs, but the *mechanics* - dispatch, combat, mobs, quests, help, registration - live in the engine itself, under `src/`. This guide walks the loop for changing one of those engine systems: find it, edit it, prove it with a unit test, rebuild, and confirm it in-game over telnet.

If your idea is content rather than mechanics - a new command, a gameplay feel experiment - you probably do not need to touch the engine at all. Reach for a [throwaway pack](prototyping-with-a-throwaway-pack.md) first. This guide is for when the behavior you want genuinely lives in C#.

The engine's JavaScript-facing API is fully typed in [`types/tapestry-engine.d.ts`](../../types/tapestry-engine.d.ts) - useful context even when you are working the C# side, because it tells you what packs can already see.

---

## The loop in one breath

1. **Find the system** in `src/Tapestry.Engine/<Subfolder>/` (Combat, Mobs, Quests, Help, Registration, ...). Read the matching `specs/<capability>.md` first - the spec is the canonical statement of current behavior.
2. **Change the code.**
3. **Add a test** in `tests/Tapestry.Engine.Tests/` - every new engine behavior needs one.
4. **Run the test:** `dotnet test --filter "FullyQualifiedName~YourTest"`.
5. **Rebuild,** stop the running server (it locks the built DLL), relaunch, and **telnet in** to exercise the change. There is no hot reload for C#.
6. **Update the spec** in the same commit, run the CONTRIBUTING build/test checks, and open a PR against `master`.

That is the whole cycle. The rest of this guide is the detail under each step.

---

## A worked example: one real change

Let us make a small, real change to the command dispatch system so the loop is concrete. The registry already resolves a verb to a handler; say we want `Resolve` to be safe against leading or trailing whitespace in the keyword so `"look "` resolves the same as `"look"`.

The system lives in `src/Tapestry.Engine/CommandRegistry.cs`. Its canonical behavior is documented in [`specs/command-dispatch.md`](../../specs/command-dispatch.md) - read that before you touch anything, because it is the source of truth for what the router already promises.

**1. The edit.** `CommandRegistry.Resolve(string keyword)` is the single-arg resolution entry point. Trim the keyword at the top:

```csharp
public CommandRegistration? Resolve(string keyword)
{
    keyword = keyword.Trim();

    // 1. Exact match (includes explicit aliases)
    if (_commands.TryGetValue(keyword, out var registrations))
    {
        return registrations.OrderByDescending(r => r.Priority).First();
    }
    // ... unchanged
}
```

**2. The test.** Tests for this file live in `tests/Tapestry.Engine.Tests/CommandRegistryTests.cs`. They follow a tiny, dependency-free pattern - construct a `CommandRegistry`, register a no-op handler, assert on `Resolve`:

```csharp
[Fact]
public void Resolve_TrimsSurroundingWhitespace()
{
    var registry = new CommandRegistry();
    registry.Register("look", _ => { }, packName: "core");

    registry.Resolve(" look ").Should().NotBeNull();
    registry.Resolve(" look ")!.Keyword.Should().Be("look");
}
```

That is the full shape of a first engine change. **Files touched:** `src/Tapestry.Engine/CommandRegistry.cs` (the behavior), `tests/Tapestry.Engine.Tests/CommandRegistryTests.cs` (the proof), and `specs/command-dispatch.md` (the spec update - covered below). Three files, one commit.

The same shape holds for a combat-number tweak: the value lives under `src/Tapestry.Engine/Combat/`, its behavior is described in [`specs/combat-resolution.md`](../../specs/combat-resolution.md), and the test goes in `tests/Tapestry.Engine.Tests/Combat/`. Find, edit, test, document - the subfolder changes, the loop does not.

---

## Finding where behavior lives

`src/Tapestry.Engine/` is organized by system. The subfolder names map directly to gameplay concepts:

```
src/Tapestry.Engine/
  Combat/         attack resolution, damage, the combat pulse
  Mobs/           mob AI, spawning, lifecycle
  Quests/         quest loading and tracking
  Help/           help topic resolution
  Registration/   the seal: how packs register commands/entities, override policy
  Abilities/  Classes/  Effects/  Economy/  Inventory/  Items/
  Login/  Persistence/  Progression/  Rest/  Prompt/  Text/  ...
```

Some systems sit at the top level rather than in a subfolder - command dispatch is `CommandRouter.cs` and `CommandRegistry.cs` directly under `src/Tapestry.Engine/`.

The fastest way to orient is not the code, it is the spec. `specs/` holds one capability file per system, and each is **the canonical statement of how that system behaves right now** - not aspiration, current reality. [`specs/README.md`](../../specs/README.md) is the index. A behavior claim in a spec carries an inline anchor pointing at the exact source lines, for example `(src/Tapestry.Engine/CommandRouter.cs:26-64)`, so the spec doubles as a map into the code.

Read the spec first. It will tell you what the system already guarantees, what it deliberately rejected (the "Rejected and Reverted" section), and where in `src/` the behavior is anchored - so you change the right thing and do not re-break a decision someone already made on purpose.

---

## Writing the required unit test

Every new engine behavior needs a unit test in `tests/Tapestry.Engine.Tests/`. This is not a nicety - it is in both `CONTRIBUTING.md` and the engine's `CLAUDE.md` conventions. The tests are the bottom of the pyramid and where most coverage belongs.

The project uses xUnit with FluentAssertions. Test files mirror the source layout: `CommandRegistryTests.cs` at the top level, `Combat/` tests under a `Combat/` folder, and so on. The pattern is small and isolated - most engine types can be constructed directly in the test with no host or DI container, as the registry tests show.

Run the whole suite, one project, or a single test:

```bash
dotnet test                                          # everything
dotnet test tests/Tapestry.Engine.Tests              # just the engine tests
dotnet test --filter "FullyQualifiedName~Resolve_TrimsSurroundingWhitespace"
```

The `--filter "FullyQualifiedName~..."` form does a substring match on the fully-qualified test name, so you can target one `[Fact]` or a whole class by name while you iterate. Tighten the loop with the filter, then run the full project once before you commit.

---

## Build gotchas that bite immediately

Three things will fail your build on day one if you do not know them.

**The SDK is dotnet 10.** `global.json` pins `10.0.300` with `rollForward: latestFeature`. A mismatched SDK produces cryptic errors that look like code problems but are not. Check `dotnet --version` first.

**Warnings are errors, everywhere.** `Directory.Build.props` sets `TreatWarningsAsErrors=true` for every project. An unused variable or a nullable-reference warning will stop the build cold. Clean up as you go; there is no warning budget.

**Central Package Management owns every version.** All NuGet versions live in `Directory.Packages.props` as `<PackageVersion ... />` entries. Individual `.csproj` files reference packages **without** a `Version` attribute. If you add `Version="x.y.z"` to a `PackageReference` in a csproj, you get a duplicate-version build error. Need a new package or a bumped version? Edit `Directory.Packages.props`, not the csproj.

---

## The CRLF trap on any telnet output path

This one is invisible on Windows and breaks on Linux, which is the worst possible failure mode, so it gets its own section.

Telnet wants `\r\n` between lines. `AppendLine` and `Environment.NewLine` emit `\r\n` on a Windows `dotnet run` but a bare `\n` on the Linux droplet. A terminal that receives a lone `\n` drops to the next line without returning to column 0, so multi-line output stair-steps to the right. A clean run on your Windows machine masks the bug entirely; it only shows up on the deployed server.

So: **when you touch any player-facing telnet output path, do not trust `AppendLine`.** Assert explicit `\r\n` in your output, or verify in the Linux container before you call it done. The systemic fix - a CRLF normalizer at `TelnetConnection.SendText` - is tracked in issue #90 and was normalized at the transport boundary in the [telnet-crlf-normalization change record](../../specs/changes/2026-06-14-telnet-crlf-normalization.md). That normalizer is idempotent and split-chunk safe, but until you can be sure your output rides that path, write `\r\n` explicitly and test for it.

---

## Verifying in-game

Tests prove the unit; telnet proves the experience. C# has no hot reload, so the in-game loop is deliberate:

1. **Rebuild:**
   ```bash
   dotnet build Tapestry.slnx -c Debug
   ```
2. **Stop the running server first.** The running process holds a lock on the built DLL, so a rebuild fails if the old server is still up. The engine runs as a plain `dotnet` process (not one named `Tapestry.Server`) - identify it by its command line when you kill it.
3. **Relaunch:**
   ```bash
   dotnet run --project src/Tapestry.Server
   ```
   The dev server loads `tapestry-core` and `example-pack` by default.
4. **Connect and exercise the change:**
   ```bash
   telnet localhost 4000
   ```
   Drive the command you changed and watch the output.

Note the asymmetry: pack JavaScript reloads on a restart with no rebuild, but a C# change needs a full rebuild. If you only changed a pack, you skip the build; if you changed the engine, you cannot.

---

## Before you open the PR

Update the spec **in the same commit as the code.** `specs/<capability>.md` is the canonical source of truth, and it is mechanically gated by spec-lint in strict mode - a stale spec or a missing source anchor fails the gate. For the worked example you would update the relevant Behavior claim in `specs/command-dispatch.md` and, if the change is significant enough to warrant one, add a change record under `specs/changes/`. Read the contract summary in [`specs/README.md`](../../specs/README.md) for the exact format rules.

Then run the CONTRIBUTING checks - both must pass before the PR:

```bash
dotnet build
dotnet test
```

`CONTRIBUTING.md` is the short version: fork, branch from `master`, change, add or update tests, make build and test pass, open a PR against `master`.

To reproduce the **full CI gate chain** locally - the same gates the pipeline runs on every push - there are two layers above the unit tests:

```bash
# Strict-boot harness: boots the engine against the stable published pack corpus.
# Run this before promoting any engine change.
node tests/tools/strict-boot-gate.js

# Telnet scenario runner: boots a real server, connects real clients, asserts on output.
# This is the top of the pyramid - reserved for stack-critical paths.
dotnet build src/Tapestry.Server -v q
node tests/tools/telnet-runner.js --all-packs --managed --clean
```

The runner's flags, scenario format, and synchronization model are documented in [`tests/README.md`](../../../tests/README.md) in the engine repo. Push coverage down to a unit test whenever you can; a new scenario needs sign-off and exists only for behavior that genuinely spans connection, login, dispatch, and output.

---

## The shape, once more

Find the system in `src/` (read the spec first), change it, add a test in `tests/Tapestry.Engine.Tests/`, `dotnet test` it, rebuild, stop-and-relaunch the server, telnet in to confirm, then update the spec and run build and test before the PR. That is your first engine change - and every one after it.
