# Tapestry Test Suite

## Unit Tests

Standard .NET test projects — this is the bottom of the pyramid and where most
coverage belongs:

```bash
dotnet test                              # everything
dotnet test tests/Tapestry.Engine.Tests  # one project
```

## Integration Scenarios (Telnet Runner)

End-to-end scenario tests that boot a real server, connect real telnet
clients, drive commands, and assert on what each client sees. This is the
very top of the pyramid: a scenario is justified only for behavior that
spans connection → login → dispatch → output (or multi-client interaction),
can't be pinned down by a unit test, and guards a stack-critical path.
Keep the count small; CI runs every scenario on every push.

### Running

```bash
# Build once, then run everything exactly like CI does
dotnet build src/Tapestry.Server -v q
node tests/tools/telnet-runner.js --all-packs --managed --clean

# One file
node tests/tools/telnet-runner.js tests/scenarios/smoke/new-player.md --managed

# Against an already-running server (no build needed)
node tests/tools/telnet-runner.js tests/scenarios/ --port 4000

# Parser/normalizer self-tests, no server
node tests/tools/telnet-runner.js --self-test
```

`--managed` boots a fresh server on a **free ephemeral port** with an
**isolated temp-dir save store** (config copied from `server.test.yaml`,
`--config`/`--packs` passed explicitly), and refuses to run scenarios until
boot is verified: every configured pack loaded, game loop started, telnet
listener bound, and the login banner answered. An empty or missing packs
directory is an immediate loud failure, never a hang.

### Flags

| Flag | Default | Description |
|------|---------|-------------|
| `--managed` | off | Boot + verify a fresh isolated server, kill it after |
| `--configuration C` | `Debug` | Which built server to run (`Debug`/`Release`); the runner does not build |
| `--packs DIR` | `<repo>/packs` | Packs corpus for the managed server |
| `--all-packs` | off | Discover `tests/scenarios/` plus `<pack>/tests/` in the packs corpus |
| `--admin-player NAME` | `Gamemaster` | Seeded admin used for `Room:` placement |
| `--scenario-timeout S` | 120 | Per-scenario wall-clock cap |
| `--suite-timeout S` | 600 | Whole-run hard cap — on expiry: dump, kill server, exit 1 |
| `--port N` | 4000 | Target port for non-managed runs |
| `--delay N` | 0 | Extra settle ms per command (sync is deterministic; rarely needed) |
| `--clean` | off | Remove old files from `results/` first |
| `--json` | off | JSON-only output |

Exit code is 0 **only** when every scenario passed — failures, errors,
timeouts, and boot problems all exit nonzero, so the runner can gate CI.
The `scenarios` job in `.github/workflows/ci.yml` runs `--all-packs
--managed` on every push against the `tapestry-packs` corpus.

### How synchronization works (no sleeps)

The engine processes each session's input FIFO on the game-loop tick. After
every command the runner queues a sentinel (`help __sync_N__`) and waits for
its echo — when it arrives, everything the command produced (including
broadcasts to other clients' sockets) has been delivered. Assertions run on
ANSI-stripped, CRLF-normalized buffers (Linux and Windows behave
identically), and negative assertions barrier the observing player first.
Every wait is bounded; there are no unconditional sleeps in the step path.

### Scenario files

```
tests/scenarios/
  _defaults/login.md   # Shared login sequence ({PlayerName}, password testpass123)
  smoke/               # Journeys: new-player, combat-pulse
  gmcp/                # Protocol: post-login burst, MSSP
  char-creation-hardening.md
  results/             # Transcripts + results.json (gitignored)
packs corpus:
  <pack>/tests/**/*.md # Pack-owned scenarios (e.g. @tapestry/example-pack), via --all-packs
```

Test players (`Wanderer`, `Alice`, `Gamemaster` — admin) are seeded by the
`@tapestry/test-fixtures` pack (`tests/fixtures/scenario-packs/`, password
`testpass123`). That pack lives in the engine repo under `tests/` precisely so
it can never be published to the registry or packaged into an image — the
runner stages it into the managed corpus at run time. **Never add accounts to
a pack under `tapestry-packs/packages`** — a committed credential in a shipped
pack is a backdoor regardless of password strength.

Format — smoke journey (one scenario per file) or command file (multiple
`## Scenario:` blocks):

```markdown
# file-name

## Scenario: What this case proves
- Players: Alice, Wanderer
- Room: same
- Skip: optional reason — scenario is reported as skipped

### Steps
1. Alice: `say hello`
2. Assert Alice sees: `You say`
3. Assert Wanderer sees: `Alice says`
4. Assert Wanderer does not see: `secret`
```

### Step types

| Syntax | Description |
|--------|-------------|
| `Player: \`command\`` | Send a command, then barrier until its output is delivered everywhere |
| `Assert Player sees: \`text\`` | Buffer contains text (bounded 2s grace) |
| `Assert Player does not see: \`text\`` | Buffer does NOT contain text (after a sync barrier) |
| `Assert Player sees one of: \`a\`, \`b\`` | At least one matches |
| `Wait for Player sees: \`text\`` | Wait up to 30s — for tick-driven events (combat pulse, weather) |
| `Assert Player receives GMCP: \`Pkg\`` | GMCP packet arrived (bounded 5s) |
| `Assert Player receives GMCP: \`Pkg\` with k="v"` | GMCP packet field match |
| `Assert \`A\` packet index is less than \`B\` packet index` | GMCP ordering |

### Setup directives

| Directive | Description |
|-----------|-------------|
| `- Players: A, B` | Named player connections (seeded test players) |
| `- Room: same` | All players recalled to spawn together (default) |
| `- Room: different` | Players 2+ move north before steps |
| `- Room: <room-id>` | Admin teleports everyone there (uses `--admin-player`) |
| `- Skip: reason` | Skip with a visible reason |

### Useful admin commands inside steps

| Command | Example |
|---------|---------|
| `spawn` | `spawn tapestry-test-fixtures:test-dummy` |
| `teleport` | `teleport Wanderer tapestry-test-fixtures:test-arena` |
| `purge` | `purge npc` |
| `loaditem` | `loaditem <item-template-id>` |

Tips:
- `tapestry-test-fixtures:test-arena` ("The Void") is the isolated combat
  room — no exits, no pre-existing mobs; `test-dummy` is a 99999-HP target.
- Use `Wait for` for anything tick-driven; plain `Assert` for direct
  command output.
- **New scenarios need sign-off** — push coverage down to unit tests first.
