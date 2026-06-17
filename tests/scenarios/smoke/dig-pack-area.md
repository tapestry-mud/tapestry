# Dig Pack Area

Verifies spec B: a builder can dig off a pack-owned room, the boundary link is
wired as a connection record, the link survives a server restart, and the shadow
guard refuses to dig in an already-occupied direction.

Also verifies the existing-exit guard on the authored carve path: digging a
direction that is already occupied from an authored room must refuse rather than
repoint the forward exit and orphan the old reverse exit as a one-way link.

Orphan detection (spec B 5.4) is covered by C# unit tests only - simulating a
pack anchor removal requires modifying pack YAML between restarts, which the
runner does not support.

## Scenario: Dig off a pack room, verify restart survival and shadow guard
- Players: Gamemaster

### Steps
1. Gamemaster: `teleport Gamemaster tapestry-test-fixtures:test-arena`
2. Assert Gamemaster sees: `The Void`
3. Gamemaster: `dig east`
4. Assert Gamemaster sees: `belongs to a pack`
5. Assert Gamemaster sees: `New Room`
6. Gamemaster: `west`
7. Assert Gamemaster sees: `The Void`
8. Gamemaster: `dig east`
9. Assert Gamemaster sees: `already taken`
10. Server: restart
11. Gamemaster: `teleport Gamemaster tapestry-test-fixtures:test-arena`
12. Assert Gamemaster sees: `The Void`
13. Gamemaster: `east`
14. Assert Gamemaster sees: `New Room`
15. Gamemaster: `dig north`
16. Assert Gamemaster sees: `into a new room`
17. Gamemaster: `south`
18. Gamemaster: `dig north`
19. Assert Gamemaster sees: `already taken`
20. Gamemaster: `dig west`
21. Assert Gamemaster sees: `already taken`
