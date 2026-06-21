# Running the Engine From a Fresh Clone

A `git clone` of the engine repo will **not boot as-is**, and that is by design. The
engine ships plumbing; the game lives in packs, and the packs are not in the engine
clone. This guide takes a fresh checkout to a running telnet server you can log into.

The engine's JavaScript API is fully typed in [`types/tapestry-engine.d.ts`](../../types/tapestry-engine.d.ts) - useful once you start touching pack scripts.

---

## Two ways to run, and which one this is

There are two completely separate paths to a running Tapestry server:

- **The player path** - the README Quick Start. You install the CLI, run
  `tapestry init` / `tapestry install` / `tapestry start`, and the CLI pulls the
  engine **Docker image** and downloads published packs from the registry. You never
  build the engine. This is for running a game, not changing the engine.
- **The engine-dev path** - `dotnet run` against the engine source and a **local
  packs corpus** on disk. This is for working on the C# engine (and on a pack
  alongside it). No Docker, no registry, no published images.

**This guide is the second path.** If you only want to play or to author a
publishable pack, follow the README Quick Start instead. If you want to spin up a
risk-free sandbox pack on top of a local engine build, see
[Prototyping with a Throwaway Pack](prototyping-with-a-throwaway-pack.md), which is a
narrower version of this same local-build technique.

---

## Prerequisites

| Tool | Version | Why |
|------|---------|-----|
| .NET SDK | `10.0.300` (pinned in [`global.json`](../../global.json), `rollForward: latestFeature`) | Builds and runs the engine. |
| Node.js | 24.x | Compiles the ESM pack scripts (`tsc`) and runs the JS tooling. |
| A telnet or MUD client | any | Connect to the running server. |

Verify the SDK pin resolves:

```bash
dotnet --version      # must satisfy global.json -> 10.0.3xx
node --version        # v24.x
```

`global.json` pins the SDK feature band, so `dotnet --version` should report a
`10.0.3xx` build. If it reports something older, install the .NET 10 SDK before going
further - the build will refuse to run on a mismatched band.

---

## Why a fresh clone will not boot as-is

Two things are deliberately missing from the checkout:

1. **`packs/` is a symlink, not a directory.** In the engine repo, `packs/` points
   into the sibling [`tapestry-packs`](https://github.com/tapestry-mud/tapestry-packs)
   tree, and that path is `.gitignore`d (`/packs/` and the plain `/packs` catch the
   symlink). A fresh clone either has a dangling link or an empty `packs/`. An empty
   or missing packs directory is a **loud boot failure, not a hang** - the engine
   fails fast at startup.
2. **`server.yaml` lists packs you do not have.** The committed config loads:

   ```yaml
   packs:
     - tapestry-core
     - legends-forgotten
   ```

   `tapestry-core` lives in the public `tapestry-packs` repo, but
   `legends-forgotten` is the private production world (the live demo) and is **not**
   part of any public clone. Boot against the committed `server.yaml` and the engine
   will fault looking for `legends-forgotten`.

The fix is two parts: get a real packs corpus on disk (next section), and override the
pack list so you load only packs you actually have (the section after).

---

## Getting a packs corpus

Clone `tapestry-packs` **beside** the engine so the `packs/` symlink resolves. The
committed symlink targets `../tapestry-packs/packages`:

```bash
# from the directory that contains your tapestry/ clone
git clone git@github.com:tapestry-mud/tapestry-packs.git
```

The published packs ship compiled JavaScript, but a **source clone does not**. Each
pack's manifest globs its scripts from a build output directory:

```yaml
# packages/@tapestry/core/pack.yaml
content:
  scripts: "dist/scripts/**/*.js"
  scripts_format: esm
```

`dist/` is `.gitignore`d in the packs repo, so a fresh clone has authored TypeScript
under `scripts/` and **nothing** under `dist/scripts/`. With no compiled scripts the
glob matches zero files and the pack loads without its behavior (or fails the boot
seal). You must compile each pack you intend to load.

Every pack carries its own `tsconfig.json` (extends the repo's `tsconfig.base.json`,
emits to `dist/scripts`). Compile a pack from its own directory:

```bash
# Unix - compile @tapestry/core
cd tapestry-packs/packages/@tapestry/core
npx tsc -p tsconfig.json
```
```powershell
# Windows
Set-Location tapestry-packs\packages\@tapestry\core
npx tsc -p tsconfig.json
```

This is exactly what the CLI does at publish time (`tsc -p tsconfig.json` per pack).
Repeat for each pack in your boot list - at minimum `@tapestry/core`, plus
`@tapestry/example-pack` if you want the starter world. After compiling, the engine's
`packs/` symlink points at a corpus that will actually load.

---

## server.local.yaml: override the pack list without editing server.yaml

Do not edit `server.yaml` to drop `legends-forgotten` - that change does not belong in
the repo. The engine reads an optional `server.local.yaml` that overrides any setting
in `server.yaml`, and that file is `.gitignore`d. Copy the example:

```bash
# Unix
cp server.local.yaml.example server.local.yaml
```
```powershell
# Windows
Copy-Item server.local.yaml.example server.local.yaml
```

The example already overrides the pack list to a corpus you have after cloning and
compiling `tapestry-packs`:

```yaml
# server.local.yaml
packs:
  - tapestry-core
  - example-pack
```

Pack ids are the manifest name with `@` dropped and `/` turned into `-`
(`@tapestry/example-pack` -> `example-pack`). Add or remove ids here to match the
packs you compiled.

---

## Launching

From the engine repo root, with `packs/` resolving and `server.local.yaml` in place:

```bash
dotnet run --project src/Tapestry.Server
```

The server reads `server.yaml`, applies `server.local.yaml` on top, and loads packs
from the `packs/` symlink. To boot from an explicit directory instead of the defaults,
pass the launch flags ([`LaunchOptions`](../../src/Tapestry.Server/LaunchOptions.cs)):

```bash
dotnet run --project src/Tapestry.Server -- \
  --config /abs/path/to/server.yaml \
  --packs  /abs/path/to/tapestry-packs/packages
```

- `--config <path>` - the server config to read (defaults to `server.yaml`). The
  `TAPESTRY_CONFIG` environment variable does the same thing.
- `--packs <dir>` - the packs corpus directory, overriding the `packs/` symlink.

The `--` separates `dotnet run` arguments from arguments passed to the engine. These
flags are how the test runner and the prototyping workflow point the engine at an
isolated boot directory; for a plain dev loop the bare `dotnet run --project
src/Tapestry.Server` is enough.

---

## What a healthy boot looks like

Watch the console log. A clean boot prints, in order:

```
Loaded pack: @tapestry/core
Loaded pack: @tapestry/example-pack
Pack validation complete: 0 issue(s) found
Telnet server listening on port 4000
Application started
```

One `Loaded pack` line per configured pack, a clean validation summary, the telnet
listener bound to port 4000 (from `server.yaml` `telnet_port`), and the host reporting
started. The server is now accepting connections.

A **sealed-shut fault** looks different - the process exits during startup with:

```
Unhandled exception ...
HostedServiceStartupFaulted ...
```

and the boot seal names what it rejected: a **located file plus a reason** (a missing
pack, a command with no help topic, a duplicate registration, a glob that matched no
scripts). This is the engine failing fast rather than booting half-configured. The two
most common first-clone faults are both covered above: `legends-forgotten` not found
(override the pack list) and a pack whose scripts did not compile (run `tsc`).

---

## Connecting

Point any telnet or MUD client at the listener:

```bash
telnet localhost 4000
```

The committed `server.yaml` has no `admin:` seed block, so there is no pre-made login.
Create a character through the interactive new-player flow at the prompt (it asks for a
name, a password, and an email - use a real-looking TLD such as `you@example.com`, the
signup path validates the format). The first commands worth running:

- `look` - the room you spawned in.
- `help` - the categorized command list (every command is gated to a help topic).
- `score` - your character sheet (stats, level, HP).

If you would rather skip signup and log straight in as an admin, add an `admin:` block
to `server.local.yaml` (handle, email, password). The engine seeds that account on
first boot - this is what the
[prototyping guide](prototyping-with-a-throwaway-pack.md) does for its one-step login.

---

## Where to go next

- **Make your first change** - the first-engine-change guide walks a small code edit
  through build, boot, and verify.
- **Get the codebase map** - the architecture orientation guide lays out the project
  graph (`Tapestry.Shared` / `Engine` / `Networking` / `Scripting` / `Data` /
  `Server`) and where game logic does and does not live.
- **Sandbox a mechanic** - [Prototyping with a Throwaway Pack](prototyping-with-a-throwaway-pack.md)
  builds a disposable pack on top of this same local engine build.
