// packs/tapestry-core/scripts/flows/link.js
// Admin flow: guided 7-step wizard for creating a room connection.

tapestry.flows.register({
    id: "admin_link",
    display_name: "linking rooms",
    trigger: "admin_link",
    cancellable: true,
    steps: [
        {
            // Step 1: Choose target pack
            id: "choose_pack",
            type: "choice",
            prompt: "Choose the pack to link to:",
            options: function() {
                var packs = tapestry.packs.getAll();

                // Sort: packs with entry-points first, then alphabetical within each group
                var withEntryPoints = [];
                var withoutEntryPoints = [];

                packs.forEach(function(p) {
                    var eps = tapestry.rooms.getEntryPoints(p.name);
                    if (eps.length > 0) {
                        withEntryPoints.push(p);
                    } else {
                        withoutEntryPoints.push(p);
                    }
                });

                var sorted = withEntryPoints.concat(withoutEntryPoints);

                return sorted.map(function(p) {
                    return {
                        label: p.displayName || p.name,
                        value: p.name,
                        tag_line: p.name,
                        description: p.description || ""
                    };
                });
            },
            on_select: function(entity, option) {
                entity.setProperty("link_pack", String(option.value));
                // Clear show-all flag when pack changes
                entity.setProperty("link_show_all", "false");
            }
        },
        {
            // Step 2: Choose target room (entry points first, with "show all" option)
            id: "choose_room",
            type: "choice",
            prompt: "Choose the destination room:",
            options: function(entity) {
                var pack = entity.getProperty("link_pack");
                var showAll = entity.getProperty("link_show_all");

                var rooms;
                if (showAll === "true") {
                    rooms = tapestry.rooms.getByPack(pack);
                    return rooms.map(function(r) {
                        return {
                            label: r.name || r.id,
                            value: r.id,
                            tag_line: r.id
                        };
                    });
                }

                // Default: entry points only, plus "Show all rooms"
                var eps = tapestry.rooms.getEntryPoints(pack);
                var options = eps.map(function(r) {
                    return {
                        label: r.name || r.id,
                        value: r.id,
                        tag_line: r.entry_point_description || r.id
                    };
                });

                options.push({ label: "Show all rooms", value: "__all__" });

                return options;
            },
            on_select: function(entity, option) {
                if (String(option.value) === "__all__") {
                    entity.setProperty("link_show_all", "true");
                    // Trigger re-display by not setting link_room --
                    // the flow engine re-evaluates options on next render.
                    // We need to stay on this step; clear link_room so confirm
                    // knows we are not done yet.
                    entity.setProperty("link_room", "");
                } else {
                    entity.setProperty("link_show_all", "false");
                    entity.setProperty("link_room", String(option.value));
                }
            }
        },
        {
            // Step 3: Choose source exit (how admin leaves their current room)
            id: "choose_source_exit",
            type: "choice",
            prompt: "Choose how players will leave this room to reach the destination:",
            options: function(entity) {
                var exits = tapestry.rooms.getExits(entity.roomId);
                var options = [];

                exits.forEach(function(e) {
                    if (e.type === "direction" && !e.occupied) {
                        options.push({
                            label: e.direction,
                            value: "direction:" + e.direction
                        });
                    }
                });

                options.push({ label: "Keyword (custom exit word)", value: "keyword" });

                return options;
            },
            on_select: function(entity, option) {
                var val = String(option.value);
                if (val.indexOf("direction:") === 0) {
                    entity.setProperty("link_src_type", "direction");
                    entity.setProperty("link_src_direction", val.slice("direction:".length));
                } else {
                    entity.setProperty("link_src_type", "keyword");
                }
            }
        },
        {
            // Step 4: Source keyword (skipped unless source type is keyword)
            id: "source_keyword",
            type: "text",
            prompt: "Enter the keyword players will type to leave (e.g. 'portal'). Optionally add a display name after a space (e.g. 'portal The Shimmering Portal'):",
            skip_if: function(entity) {
                return entity.getProperty("link_src_type") !== "keyword";
            },
            on_input: function(entity, value) {
                var parts = value.trim().split(/\s+(.*)/);
                entity.setProperty("link_src_keyword", parts[0] || "");
                entity.setProperty("link_src_display", parts[1] || "");
            }
        },
        {
            // Step 5: Choose target exit (how players leave the destination room back)
            id: "choose_target_exit",
            type: "choice",
            prompt: "Choose how players will return from the destination room:",
            options: function(entity) {
                var targetRoomId = entity.getProperty("link_room");
                var exits = tapestry.rooms.getExits(targetRoomId);

                // Find the entry-point suggested direction for this room
                var pack = entity.getProperty("link_pack");
                var eps = tapestry.rooms.getEntryPoints(pack);
                var suggestedDir = null;
                eps.forEach(function(ep) {
                    if (ep.id === targetRoomId && ep.entry_point_direction) {
                        suggestedDir = ep.entry_point_direction.toLowerCase();
                    }
                });

                var options = [];

                exits.forEach(function(e) {
                    if (e.type === "direction" && !e.occupied) {
                        var label = e.direction;
                        if (suggestedDir && e.direction.toLowerCase() === suggestedDir) {
                            label = label + " (suggested by pack author)";
                        }
                        options.push({
                            label: label,
                            value: "direction:" + e.direction
                        });
                    }
                });

                options.push({ label: "Keyword (custom exit word)", value: "keyword" });
                options.push({ label: "One-way (no return exit)", value: "one-way" });

                return options;
            },
            on_select: function(entity, option) {
                var val = String(option.value);
                if (val.indexOf("direction:") === 0) {
                    entity.setProperty("link_tgt_type", "direction");
                    entity.setProperty("link_tgt_direction", val.slice("direction:".length));
                } else if (val === "one-way") {
                    entity.setProperty("link_tgt_type", "one-way");
                } else {
                    entity.setProperty("link_tgt_type", "keyword");
                }
            }
        },
        {
            // Step 6: Target keyword (skipped unless target type is keyword)
            id: "target_keyword",
            type: "text",
            prompt: "Enter the keyword players will type to return (e.g. 'exit'). Optionally add a display name after a space:",
            skip_if: function(entity) {
                return entity.getProperty("link_tgt_type") !== "keyword";
            },
            on_input: function(entity, value) {
                var parts = value.trim().split(/\s+(.*)/);
                entity.setProperty("link_tgt_keyword", parts[0] || "");
                entity.setProperty("link_tgt_display", parts[1] || "");
            }
        },
        {
            // Step 7: Confirm summary
            id: "confirm",
            type: "confirm",
            prompt: function(entity) {
                var srcType = entity.getProperty("link_src_type") || "?";
                var tgtType = entity.getProperty("link_tgt_type") || "?";
                var targetRoom = entity.getProperty("link_room") || "?";
                var pack = entity.getProperty("link_pack") || "?";

                var srcLabel;
                if (srcType === "direction") {
                    srcLabel = entity.getProperty("link_src_direction") || "?";
                } else if (srcType === "keyword") {
                    var kw = entity.getProperty("link_src_keyword") || "?";
                    var dn = entity.getProperty("link_src_display") || "";
                    srcLabel = dn ? kw + " (" + dn + ")" : kw;
                } else {
                    srcLabel = srcType;
                }

                var tgtLabel;
                if (tgtType === "direction") {
                    tgtLabel = entity.getProperty("link_tgt_direction") || "?";
                } else if (tgtType === "keyword") {
                    var tkw = entity.getProperty("link_tgt_keyword") || "?";
                    var tdn = entity.getProperty("link_tgt_display") || "";
                    tgtLabel = tdn ? tkw + " (" + tdn + ")" : tkw;
                } else if (tgtType === "one-way") {
                    tgtLabel = "one-way, no return exit";
                } else {
                    tgtLabel = tgtType;
                }

                return "Create this connection?\r\n" +
                    "  Pack: " + pack + "\r\n" +
                    "  Destination room: " + targetRoom + "\r\n" +
                    "  Source exit - " + srcType + ": " + srcLabel + "\r\n" +
                    "  Return exit - " + tgtType + ": " + tgtLabel + "\r\n";
            }
        }
    ],
    on_complete: function(entity) {
        var fromRoomId = entity.roomId;
        var toRoomId = entity.getProperty("link_room");

        var srcType = entity.getProperty("link_src_type");
        var fromOpts = {};
        if (srcType === "direction") {
            fromOpts = { direction: entity.getProperty("link_src_direction") };
        } else if (srcType === "keyword") {
            fromOpts = {
                keyword: entity.getProperty("link_src_keyword"),
                displayName: entity.getProperty("link_src_display") || null
            };
        }

        var tgtType = entity.getProperty("link_tgt_type");
        var toOpts = {};
        if (tgtType === "direction") {
            toOpts = { direction: entity.getProperty("link_tgt_direction") };
        } else if (tgtType === "keyword") {
            toOpts = {
                keyword: entity.getProperty("link_tgt_keyword"),
                displayName: entity.getProperty("link_tgt_display") || null
            };
        }

        tapestry.connections.create(fromRoomId, srcType, fromOpts, toRoomId, tgtType, toOpts);

        return { success: true };
    }
});
