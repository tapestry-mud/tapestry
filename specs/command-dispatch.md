---
capability: command-dispatch
last-updated: 2026-06-18
---

# Command Dispatch

## Overview

The engine routes player and mob input through a single-token verb dispatch system. Packs
register commands via the JavaScript seal at boot time; the command registry resolves
the verb to a handler and invokes it.

## Behavior

- **Single-token router:** The engine routes on the first whitespace-delimited token only.
  A command registered as `look` matches `look`, `look north`, and `look at corpse` alike.
  Packs implement their own sub-dispatch for verb-noun patterns (e.g. `edit` routes to an
  edit registry).
  (src/Tapestry.Engine/CommandRouter.cs:26-64)

- **Actor-type roles vs privilege roles:** `player` and `mob` are actor-type descriptors,
  not privilege grants. They identify what kind of entity may invoke a command but never
  gate dispatch. Only named privilege roles (admin, builder, ...) gate access. A command
  with `roles: ["player", "mob"]` is available to any entity type without a privilege check.
  (src/Tapestry.Engine/CommandRouter.cs:45-61)

- **Registration carries behavior only:** Commands are registered declaratively at
  seal-load time via `tapestry.commands.register({ name, handler, roles, args, ... })`.
  A `CommandRegistration` holds keyword, handler, aliases, priority, roles, arg
  definitions, gmcp config, and visibility predicate. It no longer holds human-facing
  text: `description` and `category` were removed from the registration and dropped from
  the `Register()` signature. That text now lives in the command's help topic (see
  help-system.md). The `admin: true` shorthand is equivalent to `roles: ["admin"]` plus an
  admin privilege gate; specifying both `admin: true` and explicit `visibleTo` logs a
  warning and `admin: true` wins.
  (src/Tapestry.Engine/CommandRegistry.cs:6-24; src/Tapestry.Engine/CommandRegistry.cs:37-47; src/Tapestry.Scripting/Modules/CommandsModule.cs:122-290)

- **Command catalog accessors:** `tapestry.commands.listForPlayer(entityId)` returns the
  commands visible to a player as `{ keyword, category, description, aliases }` rows, with
  `aliases` surfaced from the registration so a catalog filter can match on them.
  `tapestry.commands.categories()` returns the visible declared category vocabulary as
  `[{ id, label }]` in declared order (hidden categories excluded). Both are read-only
  projections over already-registered data, consumed by the pack-side command catalog render.
  (src/Tapestry.Scripting/Modules/CommandsModule.cs:68-77; src/Tapestry.Scripting/Modules/CommandsModule.cs:275-313)

- **Override system:** A same-name command from two packs is a strict boot error unless one
  pack declares `{ override: true }` and has a declared dependency edge on the owning pack.
  Pack-level overrides let content packs safely extend engine-pack commands.
  (src/Tapestry.Engine/Registration/RegistrationPolicy.cs:122-180)

- **Arg resolution:** Commands may declare typed arg definitions (`args: { item: { type:
  "inventory", required: true } }`). The engine resolves them before invoking the handler;
  if resolution fails the handler is not called and the player receives an error. Custom arg
  types are registered via `tapestry.args.registerType`. The built-in `text` type is greedy
  (consumes the rest of the input line).
  (src/Tapestry.Scripting/Modules/ArgsModule.cs:28-102; src/Tapestry.Engine/ArgResolver.cs:70-146)

- **Input parsing:** The engine expands semicolons (command chaining), numeric repeats, and
  single-character non-alphanumeric aliases before dispatch. `ParseInput` is the single
  parse seam used by both the game loop's input drain and the admin `executeAs` path.
  (src/Tapestry.Engine/CommandRouter.cs:109-139; src/Tapestry.Engine/CommandInputParser.cs:9-52)

- **executeAs seam:** Admin commands may call `executeAs(actor, commandString)` to invoke a
  command in another actor's context using ROM-force semantics. The engine re-routes the
  command through `ParseInput` + `CommandRouter.Route` as if the target entity issued it
  directly: privilege checks re-gate as the target (forcing a non-admin into an admin command
  yields the target's "Huh?"), and output goes to the target's session. The invoking admin's
  privilege is not carried into the dispatched command. Session-backed players only; use
  `tapestry.mobs.command` for mob dispatch.
  (src/Tapestry.Scripting/Modules/AdminModule.cs:111-133)

- **ActorContext:** `ActorContext` is an immutable, init-only class (not a record) that
  captures the acting entity's id, name, current room, source type, raw input, resolved
  command token, and raw args for the duration of one command handler invocation. It is
  constructed by `CommandRouter` (`Route`/`RouteForMob`) from the parsed `CommandContext`
  and passed into each command handler. On the `executeAs` path
  the context is built for the target entity, so all handler code that reads the context
  operates as if the target issued the command.
  (src/Tapestry.Engine/ActorContext.cs:1-12; src/Tapestry.Engine/CommandRouter.cs:141-154)

- **GMCP auto-publish:** After every non-mob player command, the engine publishes a
  `communication.message` event on a GMCP channel (`feedback` by default, overridable per
  command). Disable per-command with `gmcp: false`.
  (src/Tapestry.Scripting/Modules/CommandsModule.cs:429-438)

## Rejected and Reverted

- None on record.

## Change Log

- 2026-06-18 [command-catalog-display](changes/2026-06-18-command-catalog-display.md)
- 2026-06-17 [command-help-registry](changes/2026-06-17-command-help-registry.md)
