---
capability: pack-security
last-updated: 2026-06-12
---

# Pack Security

## Overview

Known constraints and anti-patterns related to pack-level security in Tapestry.

## Behavior

No formal behavior spec yet. Add entries here when a change record names this capability.

## Rejected and Reverted

- **Seeding user accounts in shipped packs (TOMBSTONE):** Packs MUST NOT ship with any
  seeded user accounts -- no admin accounts, backdoor logins, or default credentials of any
  kind. The CI scenario gate caught a default admin/password seed in a shipped pack; this
  was a security defect and was removed. There is no legitimate use case for seeded
  accounts in a pack distributed via the registry or bundled with the engine.

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
