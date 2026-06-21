# Tapestry Engine Guides

Task-oriented guides for working on the Tapestry engine. These complement the
top-level `README.md` (project overview), `CONTRIBUTING.md` (workflow + ground
rules), and `specs/` (current-state capability specs - what the code does today).

New here? Read them roughly in this order:

1. **[Running the Engine From a Fresh Clone](running-the-engine-from-a-fresh-clone.md)** -
   why a clone will not boot as-is, how to assemble a local packs corpus, and the
   `dotnet run` engine-dev path to a telnet server you can log into.
2. **[Engine Architecture Orientation](engine-architecture-orientation.md)** -
   the eight projects in `Tapestry.slnx`, the boot sequence through the registration
   seal, the engine-vs-pack boundary, and a "change X, start in folder Y" lookup table.
3. **[Your First Engine Change](your-first-engine-change.md)** -
   the find -> edit -> test -> rebuild -> verify inner loop, the build gotchas
   (warnings-as-errors, Central Package Management, the CRLF trap), and the
   spec-update + CI checks before a PR.
4. **[Prototyping with a Throwaway Pack](prototyping-with-a-throwaway-pack.md)** -
   sandbox a mechanic or feel experiment in a disposable pack on top of core, touching
   zero engine game code.

The engine's JavaScript API for pack scripts is fully typed in
[`../../types/tapestry-engine.d.ts`](../../types/tapestry-engine.d.ts).
