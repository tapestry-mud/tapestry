---
release: unreleased
specs: [events.md, sessions-and-connections.md, flows-and-wizards.md]
---

# Game Entry Runs On The Game Loop

## Why

The whole telnet login sequence runs on a thread-pool thread -- `ConnectionHandler`
dispatches `LoginFlow.RunAsync` with `Task.Run`, and the web pre-auth path does the same
around `GameEntryResolver.ResolveAsync`. On that thread, committing a character into the
world published engine events directly: `FlowEngine.FinalizeCreating` published
`character.created`, `PlayerSpawner.CompleteLogin` published `player.login`, and
`ReconnectLinkDead` published `player.reconnect`.

Those publishes fan out to pack scripts, and every pack script in the process shares one
`Jint.Engine` that has no synchronization of its own. Everything the game loop does is
serialized by `Tick`, so script execution is safe there and only there -- but a login
publishing on its own thread ran concurrently with the loop's own script invocation and
could tear a Jint call in progress.

Observed on the public Threadwalker instance: a fresh character connected and the engine
logged `EventBus handler error: eventType=character.created` carrying a
`NullReferenceException` raised inside `Jint.Native.Function.ScriptFunction.Call`, below
`EventsModule.EventDispatcher.Dispatch`. The exception came from Jint's own call machinery
rather than from the pack, so the pack's handler died before its first statement and took
every effect with it: the world's login hook neither set the player's recall point nor
started its opening cutscene. That character never reached a room the game owned, and
`recall` -- the safety net -- returned it to the engine's fallback room instead of the hub.
It happened to one of ten characters created during that server's uptime, and it was
invisible to the player: no error, just a game that never started.

The same publish sites also mutated the World (adding the entity to a room, tracking it)
from off the loop, and login-time flows could invoke script through the same engine.

## What

Game entry is now posted to the loop instead of executed on the login thread.

`GameLoopEntrySpawner` is a thread barrier implementing `IGameEntrySpawner`: it wraps the
real spawner and routes `RestoreWorldObjects`, `CompleteLogin`, `TakeOverSession` and
`ReconnectLinkDead` through `GameLoop.Schedule`, which drains at the top of `Tick`. That is
the mechanism the loop already documented for work "posted from network threads to run on
game loop thread". Schedule is FIFO, so a caller that restores a player's world objects
immediately before completing the login that owns them keeps that order. Both entry points
that build a `GameEntryResolver` -- telnet and web pre-auth -- now hand it the wrapped
spawner.

The two chargen paths post the same way: `LoginFlow`'s new-character branch schedules its
`new_player_connect` flow trigger, and `PlayerSpawner.CompleteNewCharacter` schedules its
whole body. Deferring the trigger covers everything downstream of it in one move --
the flow's own script steps, alignment seeding, the world mutation in `FinalizeCreating`,
and the `character.created` publish itself.

`LoopAffinity` records whether the calling thread is inside a tick. `Tick` is fully
synchronous, so a thread-static flag answers exactly, even though `RunAsync` resumes on a
different pool thread each iteration. `EventsModule.EventDispatcher.Dispatch` consults it
and logs an error naming the event type when a script dispatch happens off the loop. This
does not change what runs where; it exists because the failure it reports is otherwise
silent to players and reads in a log as an exception from inside a third-party library
rather than as a threading violation. An engine with no loop running (unit tests, boot)
never trips it.

## Consumer

Any world pack with a `character.created` or `player.login` script hook. Threadwalker's
opener sets the player's recall point and plays its first-login cutscene from both, and was
the case that surfaced this.
