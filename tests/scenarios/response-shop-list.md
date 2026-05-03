# Scenario: Response.Shop.List

## Setup
- Player is in a room with a shopkeeper NPC
- Shopkeeper has at least one item for sale
- Client connected with: Core.Supports.Set ["Response 1"]

## Steps
1. Type: `list`

## Expected
- Terminal shows formatted item table
- GMCP event received: `Response.Shop.List`
  - `status: "ok"`
  - `shopkeeper`: shopkeeper's name string
  - `items`: array with at least one entry, each having `id` (templateId), `name`, `price`
- No `Response.Feedback` emitted for the list text

## Error case: no shop in room
1. Move to a room without a shopkeeper
2. Type: `list`

## Expected
- Terminal shows "There is no shop here."
- GMCP: `Response.Feedback` with `message: "There is no shop here."` (shop.js sends text before returning when no shop found, suppress not set)
