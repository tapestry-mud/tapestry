# Scenario: Typed Response.* commands

## Purpose
Smoke test all typed response commands. Each should emit the typed event
and NOT emit a Response.Feedback for the same text.

## Setup
- Client with GMCP: Core.Supports.Set ["Response 1"]
- Player in a room with a shopkeeper and a trainer NPC

## list
- Type: `list`
- GMCP: `Response.Shop.List` with `items` array, each item has `id`, `name`, `price`
- No `Response.Feedback` for the item table text

## buy
- Type: `buy [item name]`
- GMCP: `Response.Shop.Buy` with `status` and `message`
- No `Response.Feedback` for the buy confirmation

## sell / value
- Same pattern as buy

## practice
- Type: `practice`
- GMCP: `Response.Training.Practice` with `abilities` array
  - Each ability: `id`, `name`, `proficiency`, `cap`
- No `Response.Feedback` for the practice list

## train
- Type: `train`
- GMCP: `Response.Training.Train` with `trainsRemaining` and `stats`
- No `Response.Feedback` for the train list

## score
- Type: `score`
- GMCP: `Response.Char.Score` with full stats payload
  - Fields: `name`, `race`, `class`, `level`, `stats`, `hp`, `maxHp`, `mana`, `maxMana`, `mv`, `maxMv`, `gold`, `alignment`, `hungerTier`, `xpTracks`
- No `Response.Feedback` for the score panel

## look
- Type: `look`
- GMCP: `Response.Look` with `type: "room"`, `name`, `description`, `exits`, `entities`
- No `Response.Feedback` for the room description text

## help
- Type: `help`
- GMCP: `Response.Help` with `topic: "commands"` and `body` text
- No `Response.Feedback` for the help panel
