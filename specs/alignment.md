---
capability: alignment
last-updated: 2026-06-12
---

# Alignment

## Overview

Alignment is a numeric moral axis clamped to [-1000, 1000] stored as an integer
property on any entity. It buckets into three named ranges (evil, neutral, good)
and affects class and ability eligibility checks. Pack scripts control thresholds
and can hook into shift events.

Out of scope here: class/ability eligibility rules (character-progression.md,
abilities.md), persistence (persistence.md).

---

## Behavior

### AlignmentConfig

- `AlignmentConfig` holds two mutable thresholds: `EvilThreshold` (default -350) and
  `GoodThreshold` (default 350).
  (src/Tapestry.Engine/Alignment/AlignmentConfig.cs:6-7)

- `BucketFor(int alignment)` returns "evil" if value <= EvilThreshold, "good" if
  value >= GoodThreshold, "neutral" otherwise.
  (src/Tapestry.Engine/Alignment/AlignmentConfig.cs:15-20)

- `Configure(int evil, int good)` replaces both thresholds; no validation -- callers
  must ensure evil < good.
  (src/Tapestry.Engine/Alignment/AlignmentConfig.cs:9-13)

- `ResolveBuckets(IEnumerable<string> buckets)` converts a set of bucket name strings
  into a (min, max) numeric range resolved against the current thresholds at call time.
  The degenerate case (evil + good simultaneously) returns (null, null) --
  unrestricted. Ranges are open-ended when the boundary bucket is included.
  (src/Tapestry.Engine/Alignment/AlignmentConfig.cs:24-37)

### AlignmentRange

- `AlignmentRange` is a sealed record with optional `Min` and `Max`. `Allows(int)`
  returns false if the value is below Min or above Max; absent bound means unbounded.
  Used by other subsystems (class and ability eligibility) to gate content by score.
  (src/Tapestry.Engine/Alignment/AlignmentRange.cs:4-14)

- `AlignmentHistoryEntry` is a co-located record of
  `(long Timestamp, int Delta, string Reason, int NewValue)`.
  (src/Tapestry.Engine/Alignment/AlignmentRange.cs:17)

### AlignmentManager

- Alignment is stored in the `alignment` integer property on the entity. Absent
  property is treated as 0.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:8-9, 26)

- `Get(entityId)` returns the raw integer; `Bucket(entityId)` returns the bucket name
  and also syncs the bucket tag as a side effect.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:23-37)

- `Set(entityId, value, reason)` hard-sets alignment (clamped) with no events fired
  and no admin check. Intended for admin commands and tests.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:48-55)

- `Shift(entityId, delta, reason, context?)` fires a cancellable
  `alignment.shift.check` event. Scripts can set `cancel = true` or override
  `suggestedDelta` in the event data. Admins (entities with the `admin` role) are
  never shifted -- the method returns early before the event is published.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:60-97)

- After a successful shift, `alignment.shifted` is published with `entityId`,
  `oldValue`, `newValue`, `delta`, `reason`, and `bucketChanged`.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:116-129)

- If the bucket changes, `alignment.bucket.changed` is also published with
  `entityId`, `oldBucket`, and `newBucket`.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:131-144)

- The bucket tag (`alignment_evil`, `alignment_neutral`, or `alignment_good`) is kept
  in sync on every shift and on every `Bucket()` call.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:147-153)

- Alignment history is a rolling list capped at 20 entries, each recording
  `(Timestamp, Delta, Reason, NewValue)`. The oldest entry is dropped when the cap
  is exceeded.
  (src/Tapestry.Engine/Alignment/AlignmentManager.cs:155-162)

### JS API (alignment namespace)

- `alignment.get(entityId)` -- returns the raw integer alignment value.
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:29-31)

- `alignment.bucket(entityId)` -- returns "evil", "neutral", or "good".
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:34-37)

- `alignment.history(entityId)` -- returns an array of
  `{ timestamp, delta, reason, newValue }` objects.
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:39-51)

- `alignment.set(entityId, value, reason)` -- hard-set (admin/test use).
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:53-56)

- `alignment.shift(entityId, delta, reason)` -- normal gameplay shift path;
  triggers shift.check event.
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:58-61)

- `alignment.configure({ thresholds: { evil, good } })` -- reconfigures both
  thresholds; intended for pack startup.
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:63-74)

- `alignment.setGender(entityId, gender)` and `alignment.getGender(entityId)` write
  and read the `gender` property on the entity. These are incidental residents of the
  alignment namespace -- no alignment logic reads gender.
  (src/Tapestry.Scripting/Modules/AlignmentModule.cs:76-88)

---

## Rejected and Reverted

- None on record.

---

## Change Log
