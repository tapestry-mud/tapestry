var socials = tapestry.data.loadYaml('scripts/socials/socials.yaml');

if (!socials) {
    throw new Error('Failed to load socials.yaml');
}

socials.forEach(function(social) {
    tapestry.commands.register({
        name: social.name,
        category: 'social',
        description: social.no_target,
        args: {
            target: { type: 'visible', required: false }
        },
        handler: function(player, resolved) {
            var gender = tapestry.world.getProperty(player.entityId, 'gender');
            var reflexive = gender === 'male' ? 'himself' : gender === 'female' ? 'herself' : 'themselves';

            var target = resolved.target;

            if (!target) {
                player.send(social.no_target.replace('$n', 'You') + '\r\n');
                tapestry.world.sendToRoomExcept(
                    player.roomId,
                    player.entityId,
                    social.no_target_room.replace(/\$n/g, player.name).replace(/\$mself/g, reflexive) + '\r\n'
                );
                return;
            }

            if (target.id === player.entityId) {
                player.send(social.self.replace('$n', 'You').replace('$mself', reflexive) + '\r\n');
                tapestry.world.sendToRoomExcept(
                    player.roomId,
                    player.entityId,
                    social.self_room.replace(/\$n/g, player.name).replace(/\$mself/g, reflexive) + '\r\n'
                );
                return;
            }

            player.send(social.targeted.replace('$n', 'You').replace(/\$N/g, target.name) + '\r\n');
            tapestry.world.sendToRoomExceptMany(
                player.roomId,
                [player.entityId, target.id],
                social.targeted_room.replace(/\$n/g, player.name).replace(/\$N/g, target.name) + '\r\n'
            );
            tapestry.world.send(
                target.id,
                social.targeted_victim.replace(/\$n/g, player.name) + '\r\n'
            );
        }
    });
});
