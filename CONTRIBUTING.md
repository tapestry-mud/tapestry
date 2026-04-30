# Contributing to Tapestry

## How to contribute

1. Fork the repo and create a branch from `master`.
2. Make your changes. Add or update tests if relevant.
3. Ensure `dotnet build` and `dotnet test` pass.
4. Open a pull request against `master`.

## Development setup

```bash
dotnet run --project src/Tapestry.Server
```

Telnet to `localhost:4000`. The server loads `tapestry-core` and `example-pack` by default.

## Coding standards

- Braces on every block -- no single-line `if` bodies.
- No hardcoded game content in engine code. Mechanics go in `src/`; content goes in `packs/`.
- New engine features should include a unit test in `tests/Tapestry.Engine.Tests/`.

## Pack development

Content packs live in `packs/`. Copy `packs/example-pack/` as a starting point.

Pack manifest: `pack.yaml`. Scripts use Jint (JavaScript). See `packs/example-pack/scripts/` for examples.

## Reporting bugs

Use the GitHub issue tracker. Include steps to reproduce and the server log output.

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md).
