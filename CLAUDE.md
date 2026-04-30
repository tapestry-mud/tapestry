# Tapestry - AI Contributor Guide

## Architecture

Modular monolith. Engine (`src/`) has zero hardcoded game content. All game logic lives in content packs (`packs/`).

```
Tapestry.Shared       Enums, interfaces, shared types
Tapestry.Data         YAML loading, entity model
Tapestry.Engine       Game logic, registries, systems
Tapestry.Scripting    Jint JavaScript pack execution
Tapestry.Networking   Telnet, WebSocket, GMCP/MSSP
Tapestry.Server       Entry point, DI wiring
```

## Pack system

Packs are directories under `packs/`. Each has a `pack.yaml` manifest. Scripts are JavaScript (Jint). YAML files define areas, rooms, mobs, items.

The engine discovers and loads packs listed in `server.yaml`.

## Coding standards

- Always surround blocks with `{ }` -- no single-line execution statements.
- Engine assemblies must not reference pack-specific content by name.
- New engine behavior needs a unit test in `tests/Tapestry.Engine.Tests/`.

## Running locally

```bash
dotnet run --project src/Tapestry.Server
telnet localhost 4000
```

## Running tests

```bash
dotnet test tests/Tapestry.Engine.Tests
```

## Key files

- `server.yaml` - server config, lists which packs to load
- `packs/tapestry-core/` - minimal engine-required content (recall room, admin commands)
- `packs/example-pack/` - complete example with human race, warrior/mage classes, starter area
