---
capability: command-dispatch
last-updated: 2026-06-12
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

- **Actor-type roles vs privilege roles:** `player` and `mob` are actor-type descriptors,
  not privilege grants. They identify what kind of entity may invoke a command but never
  gate dispatch. Only named privilege roles (admin, builder, ...) gate access. A command
  with `roles: ["player", "mob"]` is available to any entity type without a privilege check.

- **Registration:** Commands are registered declaratively at seal-load time via
  `tapestry.commands.register({ name, handler, roles, args, description, ... })`.
  The `admin: true` shorthand is equivalent to `roles: ["admin"]` plus an admin privilege
  gate; specifying both `admin: true` and explicit `visibleTo` logs a warning and `admin: true`
  wins.

- **Override system:** A same-name command from two packs is a strict boot error unless one
  pack declares `{ override: true }` and has a declared dependency edge on the owning pack.
  Pack-level overrides let content packs safely extend engine-pack commands.

- **Arg resolution:** Commands may declare typed arg definitions (`args: { item: { type:
  "inventory", required: true } }`). The engine resolves them before invoking the handler;
  if resolution fails the handler is not called and the player receives an error. Custom arg
  types are registered via `tapestry.args.registerType`. The built-in `text` type is greedy
  (consumes the rest of the input line).

- **Input parsing:** The engine expands semicolons (command chaining), numeric repeats, and
  single-character non-alphanumeric aliases before dispatch. `ParseInput` is the single
  parse seam used by both the game loop's input drain and the admin `executeAs` path.

- **executeAs seam:** Admin commands may call `executeAs(actor, commandString)` to invoke a
  command in another actor's context. The privilege is the invoking admin's; the acting
  context (room, entity) is the target's.

- **GMCP auto-publish:** After every non-mob player command, the engine publishes a
  `communication.message` event on a GMCP channel (`feedback` by default, overridable per
  command). Disable per-command with `gmcp: false`.

## Rejected and Reverted

- **Multi-token routing (rejected):** Routing on two tokens was considered to reduce
  sub-dispatch boilerplate in packs. Rejected: the single-token router is simpler and the
  pack-side dispatch registry handles sub-commands cleanly. Two-token routing adds
  complexity without eliminating the need for pack dispatch.

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
