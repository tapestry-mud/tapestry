---
capability: login-and-accounts
last-updated: 2026-06-12
---

## Overview

Handles all pre-game gating from raw TCP connection through the start of play.
Covers the login state machine, account authentication, name and email
validation, the login-gate plugin API, wizlock, interactive session takeover,
and pre-auth tokens for web login.

Out of scope: save/load mechanics (persistence.md), transport and session
lifecycle (sessions-and-connections.md), the wizard-based character creation
flow (flows-and-wizards.md).

---

## Behavior

### Login phase state machine

- Phases are defined by the `LoginPhase` enum: `Connected`, `Name`, `Email`,
  `Password`, `SessionTakeover`, `Creating`, `Playing`, `LinkDead`.
  (src/Tapestry.Engine/LoginPhase.cs:4)
- `LoginFlow.RunLoginSequenceAsync` enters `Name` as its first phase.
  (src/Tapestry.Server/Login/LoginFlow.cs:88)
- Each phase has an independent `CancellationTokenSource` (`PhaseCts`).
  `SetPhase` cancels the old token and arms a new one; if a phase-specific
  timeout is configured it is applied via `CancelAfter`.
  (src/Tapestry.Server/Login/LoginFlow.cs:431-444)
- Timeouts fall back to `Idle.PreLoginTimeoutSeconds` when a phase-specific
  entry is absent or zero. (src/Tapestry.Server/Login/LoginFlow.cs:448-458)
- `SetPhase` also emits a GMCP `Login.Phase` event with the lowercase phase
  name. (src/Tapestry.Server/Login/LoginFlow.cs:443)
- On `OperationCanceledException` the flow sends "Connection timed out.",
  disconnects, and removes the pre-login session.
  On any other exception it removes the pre-login session and disconnects with
  "login error". (src/Tapestry.Server/Login/LoginFlow.cs:69-83)
- `LoginContext` holds the connection, the current phase, `ConnectedAt`, and
  the live `PhaseCts`. (src/Tapestry.Engine/Login/LoginContext.cs:1-19)

### Name validation

- Names are validated against regex `^[a-zA-Z]{2,12}$` -- letters only,
  2-12 characters. (src/Tapestry.Engine/NameValidator.cs:7)
- An engine-level block list rejects specific names with custom messages.
  The shipped block list contains exactly one entry: `"bela"`.
  (src/Tapestry.Engine/NameValidator.cs:10-18)
- Failed name input is reprompted in a loop without consuming a retry counter.
  (src/Tapestry.Server/Login/LoginFlow.cs:102-109)
- `NameValidator.Canonicalize` title-cases the accepted input (first letter
  upper, rest lower). (src/Tapestry.Engine/NameValidator.cs:40-41)
- Tests confirm empty input, single-letter input, and alphanumeric input all
  produce the "Names must be 2-12 letters only." or "Please enter a name."
  error and reprompt. (tests/Tapestry.Engine.Tests/Login/LoginFlowNameValidationTests.cs:49-101)

### Existing-player path

- If a player save exists for the canonical name, the save is loaded before
  any prompt is shown. The save carries roles needed for the wizlock check.
  (src/Tapestry.Server/Login/LoginFlow.cs:136-145)
- If wizlock is active and the loaded entity lacks the `"admin"` role, the
  flow sends `WizlockState.Message` and disconnects. The password prompt is
  never shown. (src/Tapestry.Server/Login/LoginFlow.cs:151-157)
- The flow transitions to `Password` phase and echoes are suppressed during
  input. (src/Tapestry.Server/Login/LoginFlow.cs:159-163)
- Authentication is by account ID (`AuthenticateById`), using BCrypt verify.
  (src/Tapestry.Engine/Persistence/AccountService.cs:44-58)
- After `MaxLoginAttempts` failures the flow sends "Too many failed attempts."
  and disconnects. (src/Tapestry.Server/Login/LoginFlow.cs:181-187)
- On success, a `GameEntryResolver` decides whether to spawn, reconnect, or
  initiate a session takeover. (src/Tapestry.Server/Login/LoginFlow.cs:195-218)
- `GameEntryResult.OverLimit` produces "You already have a character online.
  Disconnect first." and returns to the name prompt.
  (src/Tapestry.Server/Login/LoginFlow.cs:213-215)

### New-player path

- After name validation succeeds and no save exists, all registered login
  gates run via `LoginGateRegistry.RunAll`. A `Block` result reprompts; a
  `Ban` result sends the gate message and disconnects.
  (src/Tapestry.Server/Login/LoginFlow.cs:225-241)
- Flow transitions to `Email` phase. Up to 3 email attempts are permitted;
  the third failure disconnects. (src/Tapestry.Server/Login/LoginFlow.cs:263-270)
- `EmailValidator.Validate` requires a non-empty string containing exactly
  one `@` with at least one character before it and a domain containing a
  `.` after it. (src/Tapestry.Engine/Login/EmailValidator.cs:6-27)
- `EmailValidator.Normalize` trims and lowercases.
  (src/Tapestry.Engine/Login/EmailValidator.cs:29-32)
- If the email matches an existing account, the flow moves to `Password` phase
  and does a single password attempt (`Authenticate`). On failure it returns
  to the name prompt without disconnecting.
  (src/Tapestry.Server/Login/LoginFlow.cs:282-309)
- If the account has reached `MaxConcurrentCharacters` online the flow refuses
  with "You already have a character online. Disconnect first."
  (src/Tapestry.Server/Login/LoginFlow.cs:303-309)
- For a brand-new account, up to 3 password-creation attempts are permitted.
  The password is confirmed (typed twice); mismatch or too-short input consumes
  one attempt; the third failure disconnects.
  (src/Tapestry.Server/Login/LoginFlow.cs:316-373)
- Minimum password length comes from `config.Persistence.PasswordMinLength`.
  (src/Tapestry.Server/Login/LoginFlow.cs:332)
- `AccountService.CreateAccount` generates a new GUID, stores email
  lowercased, and hashes the password with BCrypt.
  (src/Tapestry.Engine/Persistence/AccountService.cs:14-26)
- After account resolution (new or existing), the character is registered to
  the account with `AddCharacterToAccount`.
  (src/Tapestry.Server/Login/LoginFlow.cs:311, 379)
- A name-reservation mutex prevents two concurrent connections from claiming
  the same new name; the loser is told "Someone else is creating that name
  right now." (src/Tapestry.Server/Login/LoginFlow.cs:403-418)
- On successful reservation the session phase becomes `Creating` and
  `FlowEngine.Trigger(session, "new_player_connect")` fires.
  (src/Tapestry.Server/Login/LoginFlow.cs:426-427)
- `CreateNewPlayerEntity` stamps all base stats at 10, HP/Resource/Movement
  at 100/50/100, and sets default regen rates and prompt template.
  (src/Tapestry.Server/Login/LoginFlow.cs:471-491)

### Account service

- Passwords are stored only as BCrypt hashes; `AccountService` never holds a
  plaintext password. (src/Tapestry.Engine/Persistence/AccountService.cs:20)
- `ChangePassword` requires the old password to be verified before the new
  hash is written. (src/Tapestry.Engine/Persistence/AccountService.cs:87-103)
- `AddCharacterToAccount` and `RemoveCharacterFromAccount` are idempotent
  with respect to the character list.
  (src/Tapestry.Engine/Persistence/AccountService.cs:60-85)
- An in-memory entity-to-account map (`_entityToAccount`) lets other systems
  resolve the owning account for an online entity without a store round-trip.
  (src/Tapestry.Engine/Persistence/AccountService.cs:105-118)

### Login-gate registry

- `ILoginGate.Check(canonicalName, connection)` returns a `LoginGateResult`
  with `Allowed`, optional `Message`, and `Behavior` (`Reprompt` or
  `Disconnect`). (src/Tapestry.Engine/Login/ILoginGate.cs:5-40)
- `LoginGateRegistry.RunAll` iterates gates in registration order and returns
  the first denial, or `Allow` if all pass.
  (src/Tapestry.Engine/Login/LoginGateRegistry.cs:14-25)
- Gates run on the new-player path in the telnet login flow (after name
  validation, before email prompt) and also on the `/auth/select` and
  `/auth/register` HTTP endpoints for web logins. Existing-player gating is
  handled separately (wizlock check after save load).
  (src/Tapestry.Server/Login/LoginFlow.cs:225-241;
  src/Tapestry.Server/Program.cs:356, 490;
  src/Tapestry.Server/Login/WizlockGate.cs:8-13)

### Wizlock gate

- `WizlockState` is a runtime-only singleton (`Locked` bool + const `Message`
  = "The game is wizlocked."). Not persisted; resets to unlocked on reboot.
  (src/Tapestry.Engine/Login/WizlockState.cs:1-15)
- `WizlockGate` (an `ILoginGate`) returns `Ban(WizlockState.Message)` when
  `Locked` is true, matching ROM behavior of closing the link.
  (src/Tapestry.Server/Login/WizlockGate.cs:15-33)
- For existing players the wizlock check bypasses `WizlockGate` entirely and
  runs inline after the save loads, checking for the `"admin"` role.
  (src/Tapestry.Server/Login/LoginFlow.cs:152-157)
- Tests confirm: locked + non-admin -> refused + disconnected; locked + admin
  -> proceeds to password; unlocked + non-admin -> proceeds to password.
  (tests/Tapestry.Engine.Tests/Login/LoginFlowWizlockTests.cs:109-151)

### Reserved-name gate

- `ReservedNameGate` blocks (reprompt, not disconnect) names in a static
  case-insensitive set: `self`, `me`, `all`, `here`, `nobody`, `admin`,
  `system`. (src/Tapestry.Server/Login/ReservedNameGate.cs:8-21)

### Interactive session takeover

- When `GameEntryResolver` finds a live session for the name it calls
  `ITakeoverConfirmer.ConfirmAsync`.
  (src/Tapestry.Server/Login/InteractiveTakeoverConfirmer.cs:22)
- `InteractiveTakeoverConfirmer.ConfirmAsync` cancels and replaces `PhaseCts`,
  sets phase to `SessionTakeover`, arms the phase timeout, emits GMCP
  `sessiontakeover`, and prompts "That character is already connected.
  Reconnect? (y/n)". (src/Tapestry.Server/Login/InteractiveTakeoverConfirmer.cs:44-70)
- Only the literal responses `"y"` or `"yes"` (case-insensitive) confirm.
  Any other input, or timeout, returns false.
  (src/Tapestry.Server/Login/InteractiveTakeoverConfirmer.cs:64-65)
- The `ct` argument to `ConfirmAsync` MUST be an external lifetime token, not
  derived from `_context.PhaseCts`, because `ConfirmAsync` cancels `PhaseCts`
  as its first action. Telnet callers pass `CancellationToken.None`.
  (src/Tapestry.Server/Login/InteractiveTakeoverConfirmer.cs:14-21;
  src/Tapestry.Server/Login/LoginFlow.cs:200-207)

### Pre-auth tokens

- `PreAuthSection` in server config gates the feature (`Enabled`, default
  `false`) and sets `TokenExpirySeconds` (default 60).
  (src/Tapestry.Data/PreAuthSection.cs:1-7)
- `PreAuthTokenService` issues single-use tokens stored in a
  `ConcurrentDictionary`. Each token holds name, account ID, intent
  (`Login` or `Create`), and an expiry timestamp.
  (src/Tapestry.Server/PreAuth/PreAuthTokenService.cs:1-39;
  src/Tapestry.Server/PreAuth/PreAuthToken.cs:1-27)
- `Consume` uses a check-then-act sequence: it reads the token, checks
  `IsValid` (not used AND not expired), sets `Used = true`, removes the entry,
  and returns the token. Concurrent calls can both pass the validity check
  before either marks the token used, so a single-use token may be redeemed
  more than once under concurrent load.
  (src/Tapestry.Server/PreAuth/PreAuthTokenService.cs:22-38)

### Pre-auth web path

- Issuance endpoints (`/auth/login`, `/auth/select`, `/auth/login-by-character`,
  `/auth/register`) authenticate the caller and return a token. These endpoints
  respond regardless of whether `PreAuth.Enabled` is set; the feature flag does
  not gate issuance.
  (src/Tapestry.Server/Program.cs:237-501)
- `/auth/select` and `/auth/register` run `LoginGateRegistry.RunAll` before
  issuing a Create-intent token, enforcing reserved-name and wizlock gates for
  web registrations. (src/Tapestry.Server/Program.cs:356-361, 490-495)
- Redemption occurs in the WebSocket fallback handler. The server only
  attempts token redemption when both a `token` query parameter is present
  and `config.PreAuth.Enabled` is true. An absent or expired token falls
  through to the normal telnet-style `LoginFlow`.
  (src/Tapestry.Server/Program.cs:568-703)
- On redemption of a Login-intent token the wizlock check re-runs (the flag
  may have been toggled between issuance and redemption). Create-intent tokens
  also check wizlock at redemption for the same reason, since a newly created
  character can never carry an admin role.
  (src/Tapestry.Server/Program.cs:610-686)

---

## Rejected and Reverted

- None on record.

---

## Change Log
