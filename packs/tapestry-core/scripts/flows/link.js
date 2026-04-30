// packs/tapestry-core/scripts/flows/link.js
// Admin flow: guided wizard for creating a room connection.

var OPPOSITES = {
    north: "South", south: "North",
    east: "West", west: "East",
    up: "Down", down: "Up"
};

tapestry.flows.register({
    id: "link_rooms",
    display_name: "linking rooms",
    trigger: "admin_link",
    cancellable: true,
    steps: [
        {
            // Step 1: Choose target pack (exclude packs the admin's room belongs to)
            id: "choose_pack",
            type: "choice",
            prompt: "Choose the pack to link to:",
            options: function(entity) {
                var packs = tapestry.packs.getAll();
                var currentRoomId = entity.roomId || "";

                var withEntryPoints = [];
                var withoutEntryPoints = [];

                packs.forEach(function(p) {
                    var packRooms = tapestry.rooms.getByPack(p.name);
                    var isCurrentPack = packRooms.some(function(r) { return r.id === currentRoomId; });
                    if (isCurrentPack) { return; }
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
                entity.setProperty("link_show_all", "false");
            }
        },
        {
            // Step 2a: Choose target room from entry points
            id: "choose_room_entry",
            type: "choice",
            prompt: "Choose the destination room:",
            skip_if: function(entity) {
                var pack = entity.getProperty("link_pack");
                var eps = tapestry.rooms.getEntryPoints(pack);
                var showAll = entity.getProperty("link_show_all");
                return (eps.length === 0) || (showAll === "true");
            },
            options: function(entity) {
                var pack = entity.getProperty("link_pack");
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
                    entity.setProperty("link_room", "");
                } else {
                    entity.setProperty("link_room", String(option.value));
                    entity.setProperty("link_show_all", "false");
                }
            }
        },
        {
            // Step 2b: Choose target room from all rooms
            id: "choose_room_all",
            type: "choice",
            prompt: "Choose the destination room:",
            skip_if: function(entity) {
                var room = entity.getProperty("link_room");
                return (room !== null && room !== undefined && room !== "");
            },
            options: function(entity) {
                var pack = entity.getProperty("link_pack");
                var rooms = tapestry.rooms.getByPack(pack);
                return rooms.map(function(r) {
                    return {
                        label: r.name + " (" + r.id + ")",
                        value: r.id
                    };
                });
            },
            on_select: function(entity, option) {
                entity.setProperty("link_room", String(option.value));
            }
        },
        {
            // Step 3: Choose target return exit (how players leave the destination back)
            id: "choose_target_exit",
            type: "choice",
            prompt: function(entity) {
                var targetRoom = entity.getProperty("link_room") || "destination";
                return "From " + targetRoom + ", which exit leads back to this room?";
            },
            options: function(entity) {
                var targetRoomId = entity.getProperty("link_room");
                var exits = tapestry.rooms.getExits(targetRoomId);

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
            // Step 4: Target keyword (skipped unless target type is keyword)
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
            // Step 5: Choose source exit (auto-suggest opposite of target direction)
            id: "choose_source_exit",
            type: "choice",
            prompt: function(entity) {
                var targetRoom = entity.getProperty("link_room") || "destination";
                return "From this room, which exit leads to " + targetRoom + "?";
            },
            options: function(entity) {
                var exits = tapestry.rooms.getExits(entity.roomId);
                var tgtDir = (entity.getProperty("link_tgt_direction") || "").toLowerCase();
                var suggestedSrc = OPPOSITES[tgtDir] || null;

                var options = [];

                exits.forEach(function(e) {
                    if (e.type === "direction" && !e.occupied) {
                        var label = e.direction;
                        if (suggestedSrc && e.direction === suggestedSrc) {
                            label = label + " (opposite of return exit)";
                        }
                        options.push({
                            label: label,
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
            // Step 6: Source keyword (skipped unless source type is keyword)
            id: "source_keyword",
            type: "text",
            prompt: "Enter the keyword players will type to leave (e.g. 'portal'). Optionally add a display name after a space:",
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
            // Step 7: Confirm summary
            id: "confirm",
            type: "confirm",
            on_yes: function(entity) {
                entity.setProperty("link_confirmed", "yes");
            },
            on_no: function(entity) {
                entity.setProperty("link_confirmed", "no");
            },
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
                    "  From: " + entity.roomId + " via " + srcLabel + "\r\n" +
                    "  To: " + targetRoom + " (return via " + tgtLabel + ")\r\n";
            }
        }
    ],
    on_complete: function(entity) {
        if (entity.getProperty("link_confirmed") !== "yes") {
            entity.send("Connection cancelled.\r\n");
            return { success: false };
        }

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

        entity.send("Connection created.\r\n");
        return { success: true };
    }
});
