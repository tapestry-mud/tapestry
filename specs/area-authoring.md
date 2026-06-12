---
capability: area-authoring
last-updated: 2026-06-12
---

# Area Authoring

## Overview

Known constraints and anti-patterns related to area and room authoring in Tapestry.

## Behavior

No formal behavior spec yet. Add entries here when a change record names this capability.

## Rejected and Reverted

- **Persisting runtime connection exits into side-cars or exported packs (TOMBSTONE):**
  Runtime connection exits -- exits created by the `link` command during a live session --
  MUST NOT be written into area side-car files or included in harvested/exported packs.
  These exits are runtime-only state. Persisting them causes duplication on re-load and
  import errors. Fix at the authoring write step, not at export or harvest time.

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
