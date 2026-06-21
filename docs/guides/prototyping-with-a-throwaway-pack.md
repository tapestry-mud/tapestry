# Prototyping with a Throwaway Pack

The fastest way to try a gameplay idea, a command, or a feel experiment in Tapestry is to drop a **disposable JavaScript pack** on top of `@tapestry/core`, boot the engine locally, and connect over telnet - touching **zero** of the engine's game code. When the question is answered you delete one directory and the engine and the real packs are untouched.

This is the loop the project's own combat-tempo prototype was built on. It is ideal when you are iterating on the engine and a pack *together*, or when you want a risk-free sandbox for a mechanic. If you only want to author a publishable pack (no engine changes), the lighter path is `tapestry init` a scratch project and `tapestry link` your pack - see the CLI docs. This guide covers the local-engine-build technique.

The engine's JavaScript API is fully typed in [`types/tapestry-engine.d.ts`](../../types/tapestry-engine.d.ts) - keep it open while you write.

---

## 1. An isolated boot directory

Don't author your throwaway pack inside the engine or pack repos. Make a standalone directory anywhere outside the source trees:

```
my-spike/
  server.yaml
  packs/
    @tapestry/core/      # a copy of core (ships compiled dist/scripts, boots as-is)
    @spike/my-thing/      # your throwaway pack
  data/                  # created on first run (player saves)
```

Get a copy of `@tapestry/core`. Either install it and copy from the install, or copy from a checkout of the packs monorepo:

```bash
# Unix
cp -r path/to/tapestry-packs/packages/@tapestry/core my-spike/packs/@tapestry/core
```
```powershell
# Windows (robocopy exit code 1 = "files copied" = success)
robocopy path\to\tapestry-packs\packages\@tapestry\core my-spike\packs\@tapestry\core /MIR /NFL /NDL /NJH /NJS /NC /NS /NP
```

## 2. The throwaway pack

```
packs/@spike/my-thing/
  pack.yaml
  scripts/my-thing.js     # hand-written ES module - no build step needed
  help/<command>.yaml     # one per command (the boot seal requires it)
```

**pack.yaml**
```yaml
name: "@spike/my-thing"
version: "0.0.1"
type: module
display_name: "My Spike"
description: "Throwaway prototype. Delete when done."
author: "you"
license: "AGPL-3.0"
engine: ">=0.0.1"          # permissive so the engine-version gate never blocks a spike
validation: lenient
active: true
load_order: 900            # load after core
content:
  scripts: "scripts/**/*.js"
  scripts_format: esm       # required - pack scripts are ES modules
  help: "help/**/*.yaml"
```

**scripts/my-thing.js** - every pack file is an ES module. The engine API is a namespace import; the engine loads compiled `.js` and is TypeScript-agnostic, so plain ESM works with no `tsc`:
```js
import * as tapestry from "@tapestry/engine";

tapestry.commands.register({
    name: "ping",
    roles: ["player"],
    args: { rest: { type: "text", required: false } },   // one greedy text arg; split multiword yourself
    handler: function (actor, resolved) {
        actor.send("pong: " + (resolved.rest || "") + "\r\n");   // actor.send / .entityId / .roomId / .name
    }
});
```

**help/ping.yaml** - the boot seal requires a help topic per command, with a category drawn from core's `help/categories.yaml`:
```yaml
id: "ping"
title: "Ping"
category: "combat"     # e.g. movement communication character combat inventory shop group world rest builder admin
role: "player"
keywords: [ping]
brief: "A one-line description."
body: |
  Longer description.
see_also: []           # every entry must resolve to a real topic
```

## 3. server.yaml

```yaml
server:
  name: "My Spike"
  telnet_port: 4000
  websocket_port: 4001
  tick_rate_ms: 100        # 1 tick = 100ms - the unit the schedule API counts in
packs:
  - tapestry-core          # pack id = manifest name with @ dropped and / -> -
  - spike-my-thing         # @spike/my-thing -> spike-my-thing
llm: { provider: none }
telemetry: { enabled: false }
admin:                     # seed an admin so login is one step (name -> password)
  handle: "Dev"
  email: "dev@localhost"   # the seed path is lenient about email format
  password: "changeme"
flood_protection:          # generous, so a burst of tuning commands never disconnects you
  commands_per_second: 40
  burst_size: 80
  strike_threshold: 100
  strike_decay_seconds: 2
pre_auth: { enabled: false }
persistence:
  save_path: "./data/saves"
  password_min_length: 6
```

## 4. Build and run

Build the engine once (only re-needed after C# changes):
```bash
dotnet build Tapestry.slnx -c Debug
```

Run the built server in **binary mode**, pointing it at your boot directory:
```bash
dotnet src/Tapestry.Server/bin/Debug/net10.0/Tapestry.Server.dll \
  --config /abs/path/to/my-spike/server.yaml \
  --packs  /abs/path/to/my-spike/packs
```

Connect any telnet or MUD client to `localhost:4000` and log in `Dev` / `changeme`.

**Boot succeeded** when the log shows `Loaded pack: @spike/my-thing`, `Pack validation complete: 0 issue(s) found`, `Telnet server listening on port 4000`, and `Application started`. A **fault** exits the process with `Unhandled exception` / `HostedServiceStartupFaulted` - the boot seal rejected something, and the error is located (file + reason).

Reload: pack JavaScript reloads on restart (there is no hot reload). C# changes need a rebuild - and stop the running server first, because the running process holds a lock on the built DLL.

## 5. The JavaScript seams that make a spike possible

The full surface is in [`types/tapestry-engine.d.ts`](../../types/tapestry-engine.d.ts). The load-bearing ones:

- **`tapestry.commands.register({ name, aliases, roles, args, handler })`** - dispatch is single-token; the handler receives `(actor, resolved)`. For multiword input declare one `{ type: "text" }` arg and parse it yourself. Privilege roles (`admin`, `builder`) gate the command; `player`/`mob` never do.
- **`tapestry.schedule.every(ticks, fn)`** - registers a per-tick handler at boot (a tick is `tick_rate_ms`). This is your clock. Drive a per-player state map from one global sweep rather than registering handlers at runtime. Also `everyForEach(ticks, { type: "player" }, fn)` and `cancel(handle)`.
- **`tapestry.world.send(entityId, text)`** - push text to a player from a tick, event, or flow. Also `sendToRoom`, `sendToRoomExcept`, `sendToAll`, `getProperty` / `setProperty`, `hasRole`.
- **`tapestry.flows.register({ id, trigger, steps:[...] })`** + **`tapestry.flows.trigger(entityId, trigger)`** - capture the player's next input(s) into your callbacks, then resume normal command handling. This is how you build an interactive prompt or wizard.
- **`tapestry.events.on(name, fn)` / `tapestry.events.publish(name, data)`** - the event bus.
- **Color markup:** semantic tags such as `<highlight>`, `<danger>`, `<subtle>`, `<npc>`, `<direction>`, `<system>` (defined in `core/strings/theme.yaml`) render to ANSI for capable clients and strip for plain ones. Tags may nest. All player-facing text must be 7-bit ASCII.

## 6. Iterating on feel without a person at the keyboard

To tune timing or cadence headless, drive a real telnet session from a script and timestamp the output: open a TCP socket, strip telnet IAC (`0xFF ...`) and ANSI CSI (`ESC[...`) bytes from what you read, fire scripted inputs on a stopwatch, and log each received line with a millisecond offset. The timestamps make a cadence shift visible and let you assert that an expected sequence appeared. The login flow buffers input in order, so exact send timing is forgiving.

## 7. Gotchas

- **Scripts are ES modules.** `scripts_format: esm` plus `import * as tapestry from "@tapestry/engine"`. Hand-written ESM `.js` works - no build step for a plain-JS spike.
- **The engine runs as a `dotnet` process** (not one named `Tapestry.Server`); identify it by its command line when stopping it.
- **Stop before rebuilding C#** - the running engine locks the built DLL.
- **Interactive new-character signup needs a real email TLD** (`you@example.com`, not `@localhost`); only the admin *seed* is lenient.
- **Relax `flood_protection`** or a burst of commands trips the flood guard and disconnects you.
- **Pack module state is global and shared** across players and persists until restart - fine for a solo spike; add a reset command or just relaunch.
- **The boot seal is strict even for a lenient pack:** every registered command needs a help topic with a valid category. Write the topics.

## 8. Teardown

Stop the `dotnet` process and delete the boot directory. The engine and the real packs were never touched.
