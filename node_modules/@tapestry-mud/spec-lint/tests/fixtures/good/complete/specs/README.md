# Specs

| Capability | File | Last Updated |
|------------|------|--------------|
| Feature A | [feature-a.md](feature-a.md) | 2026-01-15 |

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
