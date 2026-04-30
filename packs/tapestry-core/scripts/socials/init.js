var socials = tapestry.data.loadYaml('scripts/socials/socials.yaml');

if (!socials) {
    throw new Error('Failed to load socials.yaml');
}

socials.forEach(function(social) {
    tapestry.commands.register({
        name: social.name,
        category: 'social',
        description: social.no_target,
        handler: function(player, args) {
            var target = args.length > 0 ? args[0] : null;
            var gender = tapestry.world.getProperty(player.entityId, 'gender');
            var reflexive = gender === 'male' ? 'himself' : gender === 'female' ? 'herself' : 'themselves';

            if (!target) {
                player.send(social.no_target.replace('$n', 'You') + '\r\n');
                tapestry.world.sendToRoomExcept(
                    player.roomId,
                    player.entityId,
                    social.no_target_room.replace(/\$n/g, player.name).replace(/\$mself/g, reflexive) + '\r\n'
                );
                return;
            }

            if (target === 'self') {
                player.send(social.self.replace('$n', 'You').replace('$mself', reflexive) + '\r\n');
                tapestry.world.sendToRoomExcept(
                    player.roomId,
                    player.entityId,
                    social.self_room.replace(/\$n/g, player.name).replace(/\$mself/g, reflexive) + '\r\n'
                );
                return;
            }

            var entities = tapestry.world.getEntitiesInRoom(player.roomId, 'npc').concat(
                tapestry.world.getEntitiesInRoom(player.roomId, 'player')
            );
            var found = null;
            var lowerTarget = target.toLowerCase();
            for (var i = 0; i < entities.length; i++) {
                if (entities[i].name.toLowerCase().indexOf(lowerTarget) !== -1) {
                    found = entities[i];
                    break;
                }
            }

            if (!found) {
                player.send("They aren't here.\r\n");
                return;
            }

            player.send(social.targeted.replace('$n', 'You').replace(/\$N/g, found.name) + '\r\n');
            tapestry.world.sendToRoomExceptMany(
                player.roomId,
                [player.entityId, found.id],
                social.targeted_room.replace(/\$n/g, player.name).replace(/\$N/g, found.name) + '\r\n'
            );
            tapestry.world.send(
                found.id,
                social.targeted_victim.replace(/\$n/g, player.name) + '\r\n'
            );
        }
    });
});
