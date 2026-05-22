# Security Policy

We take the security of Tapestry and its surrounding tooling seriously. This
document explains how to report a vulnerability and what we do to keep the
project and its supply chain safe.

## Reporting a Vulnerability

**Please do not open a public issue or pull request for security problems.**
Public reports tip off attackers before a fix is available.

Instead, report privately through GitHub:

1. Go to this repository's **Security** tab.
2. Click **Report a vulnerability** (GitHub Private Vulnerability Reporting).
3. Describe the issue, the affected version or commit, and steps to reproduce.

If you can include a proof of concept, impact assessment, or suggested fix, that
helps us triage faster — but it isn't required to file a report.

### What to expect

- **A best-effort acknowledgement within 72 hours** of your report.
- An assessment and, where confirmed, a plan and rough timeline for a fix.
- **Coordinated disclosure:** we develop and release the fix before publishing
  details, then credit you in the advisory unless you'd prefer to remain
  anonymous.

This is a small, fast-moving project, so timelines are best-effort — but we will
keep you informed.

## Supported Versions

Tapestry is pre-1.0 and moves quickly. Security fixes are applied to the latest
release and the `master` branch only.

| Version            | Supported          |
| ------------------ | ------------------ |
| Latest release     | :white_check_mark: |
| `master` (default) | :white_check_mark: |
| Older releases     | :x:                |

If you're running an older tagged release, upgrade to the latest before
reporting — the issue may already be fixed.

## How We Protect the Supply Chain

Supply-chain attacks (compromised dependencies, malicious package updates,
hijacked CI) are a primary threat for any project that publishes artifacts. Our
standing measures:

- **Exact-pinned dependencies.** Every dependency is pinned to an exact version
  — no `^` or `~` ranges — so a malicious upstream release can't be pulled in
  silently by a version range.
- **Install cooldown for new packages.** Package installs enforce a minimum
  release age, so freshly published versions are not installed immediately. This
  blunts worm-style compromises that rely on rapid propagation in the hours
  after a malicious release.
- **CI actions pinned to commit SHAs.** GitHub Actions are referenced by full
  commit SHA rather than mutable tags, so a retagged or hijacked action can't
  alter our builds.
- **Least-privilege CI.** Workflows declare scoped `permissions:` blocks and use
  short-lived, narrowly scoped credentials — the built-in `GITHUB_TOKEN`,
  OpenID Connect (OIDC) tokens, and scoped GitHub App tokens — instead of
  long-lived personal access tokens or stored secrets.
- **Protected default branch.** Changes to `master` require a pull request, at
  least one review, and passing status checks before merge.

## For Contributors and Pack Authors

- **Don't introduce unpinned dependencies.** Match the existing exact-version
  pinning. PRs that add `^`/`~` ranges will be asked to pin.
- **Report suspicious dependency behavior.** If a dependency starts doing
  something unexpected — unfamiliar network calls, new lifecycle scripts,
  surprising postinstall steps — report it via the process above.
- **Treat third-party content packs as untrusted.** Packs execute JavaScript
  (via Jint) inside the engine. Only run packs you trust, and review pack
  scripts before loading them into a server you care about.

## Scope

**In scope:** the Tapestry engine, registry, CLI, web client, and the official
content-pack tooling maintained under the `tapestry-mud` organization.

**Out of scope:**

- Misconfiguration of your own self-hosted deployment (firewall rules, exposed
  admin ports, weak operator passwords, etc.).
- Third-party or community content packs not authored by this project.
- Vulnerabilities in upstream dependencies — report those upstream, though we
  appreciate a heads-up so we can pin around them.

Thank you for helping keep Tapestry and its users safe.
