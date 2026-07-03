# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and run

```bash
dotnet build                              # compile
dotnet run --project src/Tapestry.Server  # start the server
telnet localhost 4000                     # connect
```

## Tests

Three layers. Run them in order.

**Unit/integration (xUnit):**
```bash
dotnet test                                         # all projects
dotnet test tests/Tapestry.Engine.Tests             # one project
dotnet test --filter "FullyQualifiedName~MyTest"    # one test
```

**Strict-boot harness** -- boots the engine against the stable published pack corpus. Run before promoting engine changes:
```bash
node tests/tools/strict-boot-gate.js
```

**Telnet scenario runner** -- drives a live server through Markdown scenario scripts in `tests/scenarios/`:
```bash
node tests/tools/telnet-runner.js tests/scenarios/smoke/new-player.md
```

## Build gotchas

**SDK is dotnet 10** (`10.0.300`, pinned in `global.json`). A mismatched SDK produces cryptic errors; check `dotnet --version` first.

**Warnings are errors** everywhere. `Directory.Build.props` sets `TreatWarningsAsErrors=true` for all projects. New warnings break the build.

**Central Package Management.** All NuGet versions are in `Directory.Packages.props`. Do not add a `Version` attribute to individual `.csproj` references -- it causes a duplicate-version build error.

**Telnet output is a Linux-vs-Windows trap.** Newline bugs in telnet renderers reproduce on the Linux droplet but NOT on a Windows `dotnet run`: `AppendLine`/`Environment.NewLine` emits `\r\n` on Windows and a bare `\n` on Linux, and telnet needs `\r\n`, so a lone `\n` renders staggered. When touching a telnet output path, assert explicit `\r\n` (do not rely on `AppendLine`) or verify in the Linux container -- a clean Windows run masks it. Systemic fix tracked in issue #90 (CRLF normalizer at `TelnetConnection.SendText`); until it lands, this is on you.

## Layout

```
src/
  Tapestry.Shared        Enums, interfaces, shared types
  Tapestry.Contracts     Public contracts between assemblies
  Tapestry.Data          YAML loading, entity model
  Tapestry.Engine        Game logic, registries, systems
  Tapestry.Authoring     Pack authoring and validation tooling
  Tapestry.Scripting     Jint JavaScript pack execution
  Tapestry.Networking    Telnet, WebSocket, GMCP/MSSP
  Tapestry.Server        Entry point, DI wiring
tests/
  Tapestry.Engine.Tests          Engine unit tests
  Tapestry.Server.Tests          Integration tests (WebApplicationFactory)
  Tapestry.Scripting.Tests       Scripting runtime tests
  Tapestry.Networking.Tests      Networking tests
  fixtures/                      Shared test packs and data
  scenarios/                     Telnet scenario Markdown files
  tools/                         strict-boot-gate.js, telnet-runner.js
specs/                           Capability specs -- canonical source of truth for engine behavior
packs/                           Game content (only what server.yaml lists is loaded)
```

## Pack system

Packs live under `packs/`, namespaced (e.g. `packs/@tapestry/core/`). Each pack has a `pack.yaml` manifest. The server loads only packs listed in `server.yaml`.

`pack.yaml` keys are **snake_case** (`display_name`, `load_order`, `area_definitions`, etc.).

When writing engine-side Jint bindings, accept a single `JsValue` and unpack it manually. Do not bind a CLR array type directly -- Jint 4.x will not coerce a JS array to a CLR array.

## Conventions

- Always wrap control flow in braces -- no single-line body statements.
- Engine assemblies must not reference pack-specific content by name.
- New engine behavior needs a unit test in `tests/Tapestry.Engine.Tests/`.
- No unpinned dependencies (no `^` or `~` in any dependency spec).

## Behavior reference

`specs/` is the canonical source of truth for how every engine system behaves. `specs/README.md` lists all capability specs. Read specs before modifying engine behavior; update the relevant spec in the same commit.

`CONTEXT.md` at the repo root is the domain glossary. Read it before working in an unfamiliar system and use its terms exactly -- several core words (heartbeat, pulse, persistent, seal, registry, oracle) are overloaded, and its Flagged Ambiguities section is the decoder. When a change coins or sharpens a domain term, update the glossary in the same commit.

## Security

See [`SECURITY.md`](SECURITY.md). Do not report vulnerabilities in public issues or PRs.
