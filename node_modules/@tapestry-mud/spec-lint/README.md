# @tapestry-mud/spec-lint

Deterministic capability-spec linter for [Tapestry](https://github.com/tapestry-mud/tapestry) projects.

Enforces the [spec contract](https://github.com/tapestry-mud/tapestry/blob/master/specs/README.md) - eight mechanical rules checked on every commit and CI run.

## Installation

Add as a pinned devDependency (no `^` or `~`):

```json
{
  "devDependencies": {
    "@tapestry-mud/spec-lint": "0.1.0"
  }
}
```

Add an npm script:

```json
{
  "scripts": {
    "spec-lint": "spec-lint specs/"
  }
}
```

Run `npm ci` to install from the lockfile. Run `npm run spec-lint` to lint.

For C# repos with no existing package.json, add a minimal tooling manifest:

```json
{
  "private": true,
  "scripts": {
    "spec-lint": "spec-lint specs/"
  },
  "devDependencies": {
    "@tapestry-mud/spec-lint": "0.1.0"
  }
}
```

## Usage

```
npm run spec-lint           # lint specs/ in strict mode
npm run spec-lint -- --lenient   # warn only, exit 0
npm run spec-lint -- --strict    # force strict even if config says lenient
spec-lint --explain         # print the effective contract for this repo
```

## Config

Add `specs/lint.config.json` to extend or override the base rules:

```json
{
  "mode": "lenient"
}
```

Available fields:

- `mode`: `"strict"` (default) or `"lenient"`. Strict exits 1 on any violation; lenient exits 0 with warnings.
- `sections.extra`: additional required section headings beyond the base four.
- `anchorRegex`: override the pinned anchor regex (rarely needed).
- `sentinelText`: override the empty-reversal sentinel (rarely needed).

Precedence: `--strict`/`--lenient` flag > `mode` in config > default strict.

## The eight checks

<!-- spec-lint:start -->
Mode: strict

Required sections: Overview, Behavior, Rejected and Reverted, Change Log

Anchor regex (Behavior): \([@\w./\\-]+\.(cs|js|ts|json|ya?ml)(:\d+(-\d+)?)?[^)]*\)

Empty-reversal sentinel: - None on record.

Change Log: list, newest-first by date, not a table. Empty is valid for unmodified capabilities.

Index sync: every capability .md on disk appears in README index; every indexed file exists on disk; index date matches file last-updated.

Currency: for each change record naming a capability, the top Change Log entry references that record and last-updated >= record date. A capability named by zero records may have an empty Change Log.

Tombstone: a change record with status:reverted requires a tombstone entry in the capability Rejected and Reverted (not the empty sentinel).
<!-- spec-lint:end -->

## License

AGPL-3.0-only. See [LICENSE](LICENSE).
