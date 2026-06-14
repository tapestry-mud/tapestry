---
release: v0.1.30
specs: [login-and-accounts.md, sessions-and-connections.md, persistence.md]
---

# Auth-surface hardening

## Why

The web and telnet auth surfaces had drifted apart and exposed more than a
deployment needs. The HTTP credential endpoints were always mapped even on a
telnet-only server, the HTTP register path enforced no password floor while the
telnet path did, there was no cap on total concurrent connections, the container
ran as root with no liveness probe, and the admin seed password lived in
plaintext config with no env source. This slice brings the auth surface in line
with the engine's other hardened paths.

## What

- Web character-select now requires a single-use account-session token that
  proves the account login. `/auth/login` issues the token (`AccountSessionService`,
  ~120s TTL, a separate store from the WebSocket pre-auth tokens so the two
  cannot be cross-redeemed) and `/auth/select` consumes it. `/auth/login` still
  returns `account_id`, but for display only - it is no longer accepted as proof.
- The four credential endpoints (`/auth/login`, `/auth/select`,
  `/auth/login-by-character`, `/auth/register`) are mapped only when
  `pre_auth.enabled`. A telnet-only deployment exposes no web credential surface;
  the informational endpoints (`/config`, `/auth/check`, `/auth/check-email`)
  stay mapped. With pre-auth disabled the WebSocket fallback returns a bare 404
  for non-WebSocket requests.
- A single shared `PasswordValidator` enforces `Persistence.PasswordMinLength` on
  both the telnet account-creation path and HTTP `/auth/register`, so the two
  surfaces can no longer drift.
- A single `ConnectionLimiter` caps total concurrent connections at
  `Server.MaxConnections` (default 200) across telnet and WebSocket, acquired on
  accept and released symmetrically on every teardown path. Per-IP caps remain
  out of scope.
- The engine container runs as the non-root `$APP_UID` and ships a `HEALTHCHECK`
  that probes `/config`.
- The admin seed password can be supplied via the `TAPESTRY_ADMIN_PASSWORD`
  environment variable, which overrides the plaintext config value when set.
