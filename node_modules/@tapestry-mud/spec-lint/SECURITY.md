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

### What to expect

- A best-effort acknowledgement within 72 hours of your report.
- An assessment and, where confirmed, a plan and rough timeline for a fix.
- Coordinated disclosure: we develop and release the fix before publishing
  details, then credit you in the advisory unless you prefer to remain
  anonymous.

## Supported Versions

Security fixes are applied to the latest release and the `master` branch only.

## How We Protect the Supply Chain

- **Exact-pinned dependencies.** No `^` or `~` ranges.
- **CI actions pinned to commit SHAs.**
- **Least-privilege CI.** OIDC tokens; no long-lived secrets.
- **Protected default branch.** Changes require a pull request.

## Scope

**In scope:** `@tapestry-mud/spec-lint` and its test harness.

**Out of scope:** misconfiguration of your own tooling, vulnerabilities in
upstream Node.js or Jest.

Thank you for helping keep the Tapestry ecosystem safe.
