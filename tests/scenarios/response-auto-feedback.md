# Scenario: Response.Feedback auto-emit

## Purpose
Verify every command without a typed response emits Response.Feedback to clients
that declared Response 1 support.

## Setup
- Client connected with GMCP: Core.Supports.Set ["Response 1"]
- Player logged in and playing

## Steps
1. Type: `who`

## Expected
- Terminal shows player list text
- GMCP: `Response.Feedback` emitted
  - `status: "ok"`, `type: "info"`, `category: "general"`
  - `message`: contains the player list text (ANSI stripped, markup stripped)
- No duplicate `Response.Feedback` entries

## Telnet client (no GMCP support)
1. Connect with a basic telnet client that does NOT send Core.Supports.Set
2. Type: `who`

## Expected
- Terminal output only
- No GMCP events emitted
