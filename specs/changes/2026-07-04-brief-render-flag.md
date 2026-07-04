---
release: 0.1.48
specs: [scripting-runtime.md]
---

# Brief Render Flag

## Why

Brief mode v1 (tapestry#42, accessibility) needs movement-triggered room entry to render
name + exits + entity lines while explicit `look` always renders full. Movement and look
both call the single room-render chokepoint `tapestry.world.sendRoomDescription(entityId)`,
which had no way to distinguish the callers - so the engine binding gains one optional
flag rather than forking the render logic into two copies. The toggle command, player
preference property, and help topic live pack-side in @tapestry/core (release 0.1.24);
the engine ships only the render seam.

## What

- **`sendRoomDescription(entityId[, brief])`** (scripting-runtime.md).
  `ApiMessaging.SendRoomDescription` gains `bool brief = false`; `true` suppresses ONLY
  the description-body line - blank line, name, `[Exits: ...]`, and entity lines are
  byte-identical in both modes. The `WorldModule` binding widens to
  `Action<string, JsValue?>`, mapping omitted/non-boolean to full render.
  (src/Tapestry.Scripting/Services/ApiMessaging.cs;
  src/Tapestry.Scripting/Modules/WorldModule.cs;
  tests/Tapestry.Scripting.Tests/ApiMessagingTests.cs)

- **Cross-version interop pinned** (scripting-runtime.md). Jint pads a missing delegate
  arg with CLR null (NOT JsValue.Undefined) - the binding's null check is load-bearing
  for published one-arg core - and ignores extra JS args, so a two-arg core against an
  older engine degrades to full render instead of throwing. Both directions are pinned
  by tests so a Jint upgrade fails in CI before a player sees it.
  (tests/Tapestry.Scripting.Tests/JintDelegateArityTests.cs)

- **GMCP untouched by design** (scripting-runtime.md). `Room.Info` / `Room.Nearby`
  (RoomHandler off `player.moved`) and the pack-built `Response.Look` are separate
  paths and never brief. (src/Tapestry.Server/Gmcp/Handlers/RoomHandler.cs)
