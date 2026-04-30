# Tapestry Test Suite

## Integration Tests (Telnet Runner)

Scenario-based integration tests that connect to a live server via telnet and verify game behavior end-to-end.

### Running Tests

```bash
# Run all scenarios (starts/stops server automatically)
node tests/tools/telnet-runner.js tests/scenarios --managed

# Run a single file
node tests/tools/telnet-runner.js tests/scenarios/commands/combat-visibility.md --managed

# Clean old result files before running
node tests/tools/telnet-runner.js tests/scenarios --managed --clean
```

### Flags

| Flag | Default | Description |
|------|---------|-------------|
| `--managed` | off | Start a fresh server before tests, kill it after |
| `--clean` | off | Remove old transcript files from `results/` before running |
| `--port N` | 4000 | Server port |
| `--delay N` | 500 | Settle time (ms) after each command |
| `--json` | off | JSON-only output |

### Writing Scenarios

Scenarios live in `tests/scenarios/` organized by domain:

```
tests/scenarios/
  _defaults/         # Shared login sequence
  commands/          # Command tests (look, say, combat, admin)
  mobs/              # Mob behavior tests
  smoke/             # Basic connectivity
  results/           # Transcripts (gitignored)
```

Each `.md` file contains one or more scenarios:

```markdown
# file-name

## Scenario: Description of what we're testing
- Players: Alice, Bob
- Room: same

### Steps
1. Alice: `command here`
2. Assert Alice sees: `expected text`
3. Assert Bob does not see: `text`
```

### Step Types

| Syntax | Description |
|--------|-------------|
| `Player: \`command\`` | Send a command as that player |
| `Assert Player sees: \`text\`` | Check player's buffer contains text |
| `Assert Player does not see: \`text\`` | Check player's buffer does NOT contain text |
| `Assert Player sees one of: \`a\`, \`b\`` | Check buffer contains at least one of the texts |
| `Wait for Player sees: \`text\`` | Wait up to 30s for text to appear (for async events like combat ticks) |

### Setup Directives

| Directive | Description |
|-----------|-------------|
| `- Players: A, B, C` | Create named player connections |
| `- Room: same` | All players start in the same room (default) |
| `- Room: different` | Players start in different rooms |

### Admin Commands for Test Setup

Use these in scenario steps for deterministic world state:

| Command | Example | What it does |
|---------|---------|-------------|
| `spawn` | `spawn core:goblin` | Spawn a mob in your room |
| `teleport` | `teleport core:test-arena` | Teleport self to a room |
| `teleport` | `teleport Bob core:test-arena` | Teleport another player to a room |
| `purge` | `purge npc` | Remove NPCs from your room |
| `loaditem` | `loaditem core:iron-sword` | Spawn item into your inventory |
| `xpgrant` | `xpgrant self 5000` | Grant XP to a player |

### Tips

- **Use `Wait for` when testing tick-based events.** Combat rounds fire every ~4 seconds. An `Assert` right after `kill` will miss the combat output.
- **Teleport to `core:test-arena` (The Void)** for isolated tests — no exits, no pre-existing mobs.
- **Teleport to `core:training-grounds`** if you need a room that allows combat but has exits.
- **Town Square has `no-combat` tag** — don't test combat there.
- **`loaditem core:iron-sword`** + `wield sword` gives a 1d600 weapon for instant kills in tests.

## Unit Tests

Standard .NET test projects:

```bash
dotnet test tests/Tapestry.Engine.Tests
dotnet test tests/Tapestry.Scripting.Tests
dotnet test tests/Tapestry.Networking.Tests
```
