---
capability: quests
last-updated: 2026-06-12
---

# Quests

## Overview

The quest system spans `src/Tapestry.Engine/Quests/`,
`src/Tapestry.Scripting/Modules/`, `src/Tapestry.Scripting/PackLoader.cs`
(quest YAML loading), and `src/Tapestry.Server/Modules/`
(`QuestStartupModule`, `QuestScriptCoverageVerifier`). It provides a
stage-and-objective model
for player quests, event-driven objective tracking, reward dispatch on
completion, per-player YAML persistence, and a JS scripting surface for pack
authors. The engine carries no hardcoded quest content; all quest definitions
are loaded from pack `quests/` directories at startup.

Out of scope here: GMCP quest-marker delivery (see gmcp.md), pack
registration seal/conflict resolution (see registries-and-seal.md),
character save layout (see persistence.md).

---

## Behavior

### Definition Model

- A `QuestDefinition` has: `id`, `name`, `type` (default `"side"`),
  optional `giver` (NPC template ID), `repeatable` (default false),
  `abandonable` (default true), `secret` (default false), optional `script`
  path, `prereqs`, a list of `stages`, and a `rewards` block.
  (src/Tapestry.Engine/Quests/QuestDefinition.cs)

- A `QuestStage` has an `id`, `description`, optional `hint`, and a list of
  `QuestObjective` entries. (src/Tapestry.Engine/Quests/QuestStage.cs)

- A `QuestObjective` has: `id`, `type`, `target`, optional `npc`, `count`
  (default 1), and `description`.
  (src/Tapestry.Engine/Quests/QuestObjective.cs)

- `QuestPrereqs` supports: `min_level` (int), `class` (string),
  `quests_completed` (list), `quests_not_completed` (list).
  (src/Tapestry.Engine/Quests/QuestPrereqs.cs)

- `QuestReward` supports: `xp` (int), `gold` (int), `items` (list of
  template IDs), `abilities` (list of ability IDs), `class_unlock` (string),
  `race_unlock` (string). (src/Tapestry.Engine/Quests/QuestReward.cs)

### Registration

- In production, quest YAML files are loaded by `PackLoader` via the `quests:`
  glob in each pack manifest. `PackLoader.LoadQuests` iterates the matched
  files, deserializes each as a `QuestDefinition` via `YamlContentLoader`,
  sets `PackDirectory` to the pack root, then registers the definition via
  `QuestRegistry.Register`. Malformed files log a warning and are skipped;
  they do not abort pack loading. (PackLoader.cs:246)

- Objectives that have no explicit `id` in YAML are auto-assigned an ID of
  `{stage.id}-{obj.type}-{index}`. (QuestRegistry.cs:47)

- `QuestRegistry.Register(quest)` is the runtime/test entry point for
  adding quests programmatically; it also runs the auto-ID pass.
  (QuestRegistry.cs:39)

- Each loaded `QuestDefinition` has its `PackDirectory` set to the pack
  root so the coverage verifier can resolve relative `script:` paths.
  (QuestRegistry.cs:28; commit 0f428ec)

### Accept / Lifecycle

- `QuestService.AcceptQuest(player, questId)` is the single entry point for
  granting a quest. It returns an `AcceptQuestResult` enum:
  `Accepted`, `AlreadyActive`, `AlreadyCompleted`, `PrereqNotMet`,
  `CapReached`, or `NotFound`. (QuestService.cs:50)

- Prereq evaluation order: min level, then class, then quests_completed, then
  quests_not_completed. Level is read from the `"main"` key of the entity's
  level map; missing level is treated as 1. (QuestService.cs:323)

- The active-quest cap (`QuestConfig.ActiveCap`) applies only to abandonable
  quests. Non-abandonable quests always bypass the cap.
  (QuestService.cs:75; QuestServiceTests.cs:AcceptQuest_NonAbandonable_BypassesCap)

- On accept, `StageIndex` is set to 0 and objective states are initialized
  with `Current = 0`, `Required = obj.Count`.
  (QuestService.cs:91; QuestServiceTests.cs:AcceptQuest_InitializesObjectiveStates)

- A `quest.started` event is published on every successful accept. The event
  includes a `bannerText` field unless the quest is `secret`, the call was
  `silent: true`, or the script's `onGranted` hook returned true.
  (QuestService.cs:106;
  QuestServiceTests.cs:AcceptQuest_IncludesBannerText_InQuestStartedEvent_WhenNotSecret,
  AcceptQuest_NoBannerText_InEvent_WhenSilent,
  AcceptQuest_NoBannerText_InEvent_WhenQuestIsSecret,
  AcceptQuest_NoBannerText_InEvent_WhenScriptOnGrantedReturnsTrue)

- State is persisted immediately after every successful accept.
  (QuestService.cs:134; QuestServiceTests.cs:AcceptQuest_SavesQuestState)

- The banner is a fixed-width 46-character box showing quest name, type,
  the current stage description (word-wrapped), and per-objective progress
  counters. (QuestService.cs:363)

### Objective Advancement

- `QuestService.AdvanceObjective(playerId, questId, objectiveId, amount)`
  clamps `Current` to `Required` and calls the `onObjectiveAdvanced` script
  hook and publishes `quest.objective.advanced`.
  Already-complete objectives are silently skipped. (QuestService.cs:139)

- When all objectives in the current stage are complete and a next stage
  exists, `AdvanceStage` replaces objective states, fires the
  `onStageAdvanced` script hook, and publishes `quest.stage.advanced`.
  (QuestService.cs:267; QuestServiceTests.cs:AdvanceObjective_AdvancesStage_WhenAllObjectivesInStageMet)

- When the last stage completes, `CompleteQuest` is called: the active entry
  is removed, the quest ID is added to the `Completed` set, rewards are
  dispatched, the `onCompleted` hook fires, `quest.completed` is published,
  and state is persisted.
  (QuestService.cs:292; QuestServiceTests.cs:AdvanceObjective_CompletesQuest_WhenAllObjectivesMet)

- `AdvanceMatchingObjectives(playerId, objectiveType, predicate)` fans out
  across all active quests in the current stage, advancing any objective
  whose `Type` matches and whose definition passes the predicate.
  (QuestService.cs:196)

### Objective Types and Watcher

- `QuestObjectiveWatcher` subscribes to four events at startup:
  - `mob.killed` -- advances `kill` objectives matching `templateId`.
    (QuestObjectiveWatcher.cs:37)
  - `entity.item.picked_up` -- advances `collect` objectives matching
    `templateId`; also triggers `quest_grant` item property.
    (QuestObjectiveWatcher.cs:44)
  - `entity.item.given` -- advances `deliver` objectives matching both
    `Target` (item template) and `Npc` (receiver template).
    (QuestObjectiveWatcher.cs:78)
  - `player.moved` -- advances `visit` objectives matching `new_room_id`;
    also triggers `quest_grant` room property. (QuestObjectiveWatcher.cs:88)

- Items carrying a `quest_grant` property value auto-grant the named quest
  when the player picks them up. Rooms carrying a `quest_grant` property
  auto-grant the named quest on entry. (QuestObjectiveWatcher.cs:51, 95;
  commit 295ee0e)

- Items may carry a `quest_advance` event data value in format
  `{packId}:{questId}:{objectiveId}`, triggering a direct single-increment
  advance on pickup. (QuestObjectiveWatcher.cs:61)

### Abandon

- `AbandonQuest(playerId, questId)` silently does nothing if the quest
  definition has `Abandonable = false` or is not found.
  (QuestService.cs:233; QuestServiceTests.cs:AbandonQuest_Ignored_WhenNotAbandonable)

- On successful abandon, the active entry is removed, `quest.abandoned` is
  published, and state is persisted. (QuestService.cs:247)

### Reward Dispatch

- `QuestRewardDispatcher.Dispatch` applies rewards in this order: xp, gold,
  abilities, class_unlock, race_unlock, items.
  (QuestRewardDispatcher.cs:29)

- XP is granted via `IQuestProgressionService.GrantExperience` on the
  `"main"` track with source `"quest"`. (QuestRewardDispatcher.cs:33)

- Gold is added via `IQuestCurrencyService.AddGold` with reason
  `"quest_reward"`. (QuestRewardDispatcher.cs:38)

- Each ability is taught via `IQuestProficiencyService.Learn` at initial
  proficiency 1. (QuestRewardDispatcher.cs:43)

- `class_unlock` and `race_unlock` set `"class"` and `"race"` properties
  directly on the player entity. (QuestRewardDispatcher.cs:47)

- Item rewards are created via `IQuestItemRegistry.CreateItem` and silently
  placed in inventory. Null items (unknown templates) are skipped.
  (QuestRewardDispatcher.cs:57)

- Rewards require the player entity to be in `_playerCache`. `_playerCache` is
  populated in `AcceptQuest` (QuestService.cs:89) but is never repopulated on
  player login. After a server restart, `CompleteQuest` skips reward dispatch
  (the player entity is absent from the cache) while still publishing
  `quest.completed` -- rewards are permanently lost for non-repeatable quests
  completed post-restart. The player name fallback also causes save files to
  land at `players/{guid}/quests.yaml` rather than the correct path.
  (QuestService.cs:297)

### Persistence

- Quest state is persisted to
  `{savePath}/players/{playerName_lowercase}/quests.yaml`.
  (QuestPersistenceService.cs:113; commit 642fe96)

- On `player.login`, the persistence service loads the file and sets the
  in-memory `QuestStateRepository` entry. Login detection requires a
  `playerName` field in the event data. (QuestPersistenceService.cs:54)

- On load, orphan removal strips active and completed entries whose quest IDs
  are no longer in the registry. Orphan removal is skipped if the registry
  is empty, to avoid wiping all quests before packs have loaded.
  (QuestPersistenceService.cs:98)

- YAML is serialized/deserialized with underscored naming conventions.
  `QuestObjectiveState.Complete` is `[YamlIgnore]` and is always derived
  from `Current >= Required`. (QuestObjectiveState.cs:14)

### Quest Marker Service

- `QuestMarkerService.HasQuestMarker(playerId, templateId)` returns true if
  the template matches any active non-secret quest's giver, a current-stage
  `deliver` objective's `Npc`, or a current-stage `collect` objective's
  `Target`. (QuestMarkerService.cs:20)

- Kill objective targets are intentionally excluded from markers to avoid
  marking every hostile mob in the area.
  (QuestMarkerService.cs:67 comment)

- `GetQuestMarkersForRoom` returns a list of `QuestMarker(entityId,
  templateId, questId)` records for room entities that are quest targets.
  (QuestMarkerService.cs:33)

### Script Coverage Verifier

- `QuestScriptCoverageVerifier.Verify()` runs at boot (before
  `QuestPersistenceService.Start()` and `QuestObjectiveWatcher.Start()`).
  (QuestStartupModule.cs:35)

- For every quest with a `script:` field and a non-null `PackDirectory`,
  the verifier resolves the absolute path and checks: (a) the file exists on
  disk and (b) it was executed by the pack's `scripts:` glob
  (`JintRuntime.HasExecutedFile`). (QuestScriptCoverageVerifier.cs:37)

- A strict pack (the default) throws `InvalidOperationException` on any
  failure, halting boot. A `validation: lenient` pack emits a warning log
  instead. (QuestScriptCoverageVerifier.cs:64)

- Both missing-file and unglobbed-but-present cases are treated identically
  as hard errors for strict packs. (QuestScriptCoverageVerifier.cs:52)

### Pack JS API (`tapestry.quests`)

- `quests.offer(entityId, questId, options?)` -- calls `AcceptQuest`; the
  `options.silent` boolean suppresses the banner.
  (QuestModule.cs:36)

- `quests.isActive(entityId, questId)` -- returns boolean.
  (QuestModule.cs:66)

- `quests.isCompleted(entityId, questId)` -- returns boolean.
  (QuestModule.cs:76)

- `quests.getState(entityId)` -- returns an object with an `active` array.
  Each entry includes `questId`, `name`, `type`, `stageIndex`, `stageCount`,
  `stageDescription`, and an `objectives` array with per-objective
  `objectiveId`, `description`, `current`, `required`, `complete`.
  (QuestModule.cs:86)

- `quests.abandon(entityId, questId)` -- calls `AbandonQuest`.
  (QuestModule.cs:130)

- `quests.getHint(entityId, questId)` -- returns the current stage's `hint`
  string, or null. (QuestModule.cs:140)

- `quests.getActiveHints(entityId)` -- returns an array of hint strings for
  all active quests that have a hint on the current stage.
  (QuestModule.cs:159)

- `quests.hasQuestMarker(entityId, templateId)` -- delegates to
  `QuestMarkerService`. (QuestModule.cs:186)

- `quests.registerScript(questId, hooksObj)` -- registers lifecycle hooks
  (`onGranted`, `onObjectiveAdvanced`, `onCompleted`, `onStageAdvanced`)
  under the registration-policy seal. A duplicate without `override: true`
  is a boot error on strict packs. (QuestModule.cs:202)

- `quests.registerObjectiveType(type, handler)` -- currently a no-op
  placeholder; full wiring is not yet implemented.
  (QuestModule.cs:196-200)

---

## Rejected and Reverted

- **Legacy `script:` executor** -- the `QuestStartupModule` previously ran
  a dedicated script loader driven by each quest's `script:` field. This was
  replaced: the single `scripts:` glob in the pack manifest is now the only
  quest-script executor. The `script:` field is retained solely as a
  coverage-verification hint. (QuestStartupModule.cs:26 comment;
  commit f0d8f14 "Unify quest script execution into PackLoader (#109)")

---

## Change Log

| Release | Record | Summary |
|---------|--------|---------|
