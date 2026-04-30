// packs/tapestry-core/scripts/flows/unlink.js
// Admin flow: remove a connection from the current room.

tapestry.flows.register({
    id: "unlink_rooms",
    display_name: "unlinking rooms",
    trigger: "admin_unlink",
    cancellable: true,
    steps: [
        {
            // Step 1: Choose connection to remove
            id: "choose_connection",
            type: "choice",
            prompt: "Choose a connection to remove:",
            options: function(entity) {
                var connections = tapestry.connections.getForRoom(entity.roomId);

                if (connections.length === 0) {
                    return [
                        {
                            label: "No connections to remove. Type 'quit' to cancel.",
                            value: "__none__"
                        }
                    ];
                }

                return connections.map(function(conn) {
                    var fromRoom = conn.from.room || "?";
                    var toRoom = conn.to.room || "?";
                    var fromLabel;

                    if (conn.from.direction) {
                        fromLabel = conn.from.direction;
                    } else if (conn.from.keyword) {
                        fromLabel = "enter " + conn.from.keyword;
                    } else {
                        fromLabel = "?";
                    }

                    var label = "from " + fromRoom + " via " + fromLabel + " to " + toRoom;

                    return {
                        label: label,
                        value: conn.id
                    };
                });
            },
            on_select: function(entity, option) {
                entity.setProperty("unlink_id", String(option.value));
            }
        },
        {
            // Step 2: Confirm removal
            id: "confirm_unlink",
            type: "confirm",
            prompt: "Remove connection [y/n]?"
        }
    ],
    on_complete: function(entity) {
        var unlinkId = entity.getProperty("unlink_id");

        if (unlinkId === "__none__") {
            return { success: true };
        }

        tapestry.connections.remove(unlinkId);
        entity.send("Connection removed.\r\n");
        return { success: true };
    }
});
