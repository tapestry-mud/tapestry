var directions = ['north', 'south', 'east', 'west', 'up', 'down'];

var opposites = {
    north: 'the south', south: 'the north',
    east: 'the west',  west: 'the east',
    up: 'below',       down: 'above'
};

function generateGroupId() {
    return 'grp_' + Date.now().toString(36) + '_' + Math.floor(Math.random() * 0xfffff).toString(36);
}

function getGroupId(entityId) {
    return tapestry.world.getProperty(entityId, 'group_id');
}

function getGroupLeaderId(entityId) {
    return tapestry.world.getProperty(entityId, 'group_leader');
}

function isInGroup(entityId) {
    return !!getGroupId(entityId);
}

function isGroupLeader(entityId) {
    return getGroupLeaderId(entityId) === entityId;
}

function getGroupMembers(entityId) {
    var groupId = getGroupId(entityId);
    if (!groupId) { return []; }
    var online = tapestry.world.getOnlinePlayers();
    var members = [];
    for (var i = 0; i < online.length; i++) {
        if (tapestry.world.getProperty(online[i].id, 'group_id') === groupId) {
            members.push(online[i].id);
        }
    }
    return members;
}

function getSameRoomGroupMembers(entityId) {
    var roomId = tapestry.world.getEntityRoomId(entityId);
    var members = getGroupMembers(entityId);
    var result = [];
    for (var i = 0; i < members.length; i++) {
        if (members[i] !== entityId && tapestry.world.getEntityRoomId(members[i]) === roomId) {
            result.push(members[i]);
        }
    }
    return result;
}

function addToGroup(entityId, leaderId, groupId) {
    tapestry.world.setProperty(entityId, 'group_id', groupId);
    tapestry.world.setProperty(entityId, 'group_leader', leaderId);
    tapestry.world.setProperty(entityId, 'group_join_time', Date.now());
}

function removeFromGroup(entityId) {
    tapestry.world.setProperty(entityId, 'group_id', null);
    tapestry.world.setProperty(entityId, 'group_leader', null);
    tapestry.world.setProperty(entityId, 'group_join_time', null);
}

function sendToGroup(senderEntityId, message) {
    var members = getGroupMembers(senderEntityId);
    for (var i = 0; i < members.length; i++) {
        tapestry.world.send(members[i], message);
    }
}

function promoteNextLeader(departingLeaderId, remainingMembers) {
    if (!remainingMembers || remainingMembers.length === 0) { return null; }
    var earliest = null;
    var earliestTime = Infinity;
    for (var i = 0; i < remainingMembers.length; i++) {
        var joinTime = tapestry.world.getProperty(remainingMembers[i], 'group_join_time') || 0;
        if (joinTime < earliestTime) {
            earliestTime = joinTime;
            earliest = remainingMembers[i];
        }
    }
    if (!earliest) { return null; }
    for (var j = 0; j < remainingMembers.length; j++) {
        tapestry.world.setProperty(remainingMembers[j], 'group_leader', earliest);
    }
    return earliest;
}

function getPlayerName(entityId) {
    var online = tapestry.world.getOnlinePlayers();
    for (var i = 0; i < online.length; i++) {
        if (online[i].id === entityId) { return online[i].name; }
    }
    return null;
}

function sendToGroupExcept(selfId, alsoSkipId, message) {
    var members = getGroupMembers(selfId);
    for (var i = 0; i < members.length; i++) {
        if (members[i] !== selfId && members[i] !== alsoSkipId) {
            tapestry.world.send(members[i], message);
        }
    }
}

tapestry.commands.register({
    name: 'follow',
    description: 'Follow a player or stop following. Usage: follow [player] | follow stop',
    category: 'movement',
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Follow whom? Usage: follow [player] | follow stop\r\n');
            return;
        }

        if (args[0].toLowerCase() === 'stop') {
            var leaderId = tapestry.world.getProperty(player.entityId, 'following');
            if (!leaderId) {
                player.send('You are not following anyone.\r\n');
                return;
            }
            tapestry.world.setProperty(player.entityId, 'following', null);
            var leaderName = getPlayerName(leaderId);
            if (leaderName) {
                player.send('You stop following ' + leaderName + '.\r\n');
                tapestry.world.send(leaderId, player.name + ' stops following you.\r\n');
            } else {
                player.send('You stop following them.\r\n');
            }
            tapestry.events.publish('follow.ended', {
                followerId: player.entityId,
                leaderId: leaderId,
                reason: 'command'
            });
            return;
        }

        var targetName = args[0];
        if (targetName.toLowerCase() === player.name.toLowerCase()) {
            player.send('You cannot follow yourself.\r\n');
            return;
        }

        var target = tapestry.world.findPlayerByName(targetName);
        if (!target) {
            player.send(targetName + ' is not online.\r\n');
            return;
        }

        if (tapestry.world.getProperty(target.id, 'no_follow')) {
            player.send(target.name + ' is not accepting followers.\r\n');
            return;
        }

        var currentFollowing = tapestry.world.getProperty(player.entityId, 'following');
        if (currentFollowing === target.id) {
            player.send('You are already following ' + target.name + '.\r\n');
            return;
        }

        tapestry.world.setProperty(player.entityId, 'following', target.id);
        player.send('You begin following ' + target.name + '.\r\n');
        tapestry.world.send(target.id, player.name + ' begins following you.\r\n');
        tapestry.events.publish('follow.started', {
            followerId: player.entityId,
            leaderId: target.id
        });
    }
});

tapestry.commands.register({
    name: 'nofollow',
    description: 'Toggle whether others can follow you. When active, drops current followers.',
    category: 'movement',
    handler: function(player, args) {
        var current = tapestry.world.getProperty(player.entityId, 'no_follow');
        if (current) {
            tapestry.world.setProperty(player.entityId, 'no_follow', null);
            player.send('You are now accepting followers.\r\n');
        } else {
            tapestry.world.setProperty(player.entityId, 'no_follow', true);
            player.send('You are no longer accepting followers.\r\n');
            var online = tapestry.world.getOnlinePlayers();
            for (var i = 0; i < online.length; i++) {
                var followerId = online[i].id;
                if (followerId === player.entityId) { continue; }
                if (tapestry.world.getProperty(followerId, 'following') === player.entityId) {
                    tapestry.world.setProperty(followerId, 'following', null);
                    tapestry.world.send(followerId,
                        player.name + ' is no longer accepting followers.\r\n');
                    tapestry.events.publish('follow.ended', {
                        followerId: followerId,
                        leaderId: player.entityId,
                        reason: 'nofollow'
                    });
                }
            }
        }
    }
});

tapestry.events.on('player.direction.moved', function(event) {
    var data = event.data || {};
    var leaderId = data.entityId;
    var leaderName = data.leaderName;
    var direction = data.direction;
    var fromRoom = data.fromRoom;
    var arrivalFrom = data.arrivalFrom;

    if (!leaderId || !direction || !fromRoom) { return; }

    var online = tapestry.world.getOnlinePlayers();
    for (var i = 0; i < online.length; i++) {
        var followerId = online[i].id;
        if (followerId === leaderId) { continue; }

        if (tapestry.world.getProperty(followerId, 'following') !== leaderId) { continue; }

        var followerRoom = tapestry.world.getEntityRoomId(followerId);
        if (followerRoom !== fromRoom) { continue; }

        var restState = tapestry.rest.getRestState(followerId);
        if (restState === 'resting' || restState === 'sleeping') { continue; }

        if (tapestry.combat.isInCombat(followerId)) {
            tapestry.world.send(followerId, 'You cannot follow while in combat.\r\n');
            continue;
        }

        var followerName = online[i].name;
        var moved = tapestry.world.moveEntity(followerId, direction);
        if (moved) {
            var newRoom = tapestry.world.getEntityRoomId(followerId);
            tapestry.world.send(followerId, 'You follow ' + leaderName + ' ' + direction + '.\r\n');
            tapestry.world.sendRoomDescription(followerId);
            tapestry.world.triggerDisposition(followerId);
            tapestry.world.sendToRoomExceptSleeping(
                fromRoom, followerId, followerName + ' leaves ' + direction + '.\r\n');
            tapestry.world.sendToRoomExceptSleeping(
                newRoom, followerId, followerName + ' arrives from ' + arrivalFrom + '.\r\n');
        }
    }
});

function clearFollowState(entityId) {
    var leaderId = tapestry.world.getProperty(entityId, 'following');
    if (leaderId) {
        tapestry.world.setProperty(entityId, 'following', null);
        tapestry.events.publish('follow.ended', {
            followerId: entityId,
            leaderId: leaderId,
            reason: 'cleanup'
        });
    }

    var online = tapestry.world.getOnlinePlayers();
    for (var i = 0; i < online.length; i++) {
        var followerId = online[i].id;
        if (followerId === entityId) { continue; }
        if (tapestry.world.getProperty(followerId, 'following') === entityId) {
            tapestry.world.setProperty(followerId, 'following', null);
            tapestry.world.send(followerId, 'Your leader is gone. You stop following.\r\n');
            tapestry.events.publish('follow.ended', {
                followerId: followerId,
                leaderId: entityId,
                reason: 'cleanup'
            });
        }
    }
}

tapestry.events.on('player.logout', function(event) {
    var entityId = event.sourceEntityId;
    if (!entityId) { return; }
    tapestry.world.setProperty(entityId, 'no_follow', null);
    clearFollowState(entityId);
    if (isInGroup(entityId)) {
        handleGroupLeave({ entityId: entityId, name: getPlayerName(entityId) || 'Someone', send: function() {} });
    }
});

tapestry.events.on('player.death', function(event) {
    var data = event.data || {};
    var entityId = data.entityId;
    if (!entityId) { return; }
    clearFollowState(entityId);
});

tapestry.events.on('player.teleported', function(event) {
    var data = event.data || {};
    var entityId = data.entityId;
    if (!entityId) { return; }
    clearFollowState(entityId);
});

tapestry.commands.register({
    name: 'group',
    aliases: ['gr'],
    description: 'Manage your group. Subcommands: invite, accept, decline, leave, kick, promote, disband',
    category: 'group',
    handler: function(player, args) {
        var sub = args[0] ? args[0].toLowerCase() : '';
        if (sub === 'invite') { handleGroupInvite(player, args.slice(1)); }
        else if (sub === 'accept') { handleGroupAccept(player); }
        else if (sub === 'decline') { handleGroupDecline(player); }
        else if (sub === 'leave') { handleGroupLeave(player); }
        else if (sub === 'kick') { handleGroupKick(player, args.slice(1)); }
        else if (sub === 'promote') { handleGroupPromote(player, args.slice(1)); }
        else if (sub === 'disband') { handleGroupDisband(player); }
        else { handleGroupList(player); }
    }
});

function handleGroupInvite(player, args) {
    if (args.length === 0) {
        player.send('Invite whom?\r\n');
        return;
    }
    var targetName = args[0];
    var target = tapestry.world.findPlayerByName(targetName);
    if (!target) {
        player.send(targetName + ' is not online.\r\n');
        return;
    }
    if (target.id === player.entityId) {
        player.send('You cannot invite yourself.\r\n');
        return;
    }
    if (isInGroup(target.id)) {
        player.send(target.name + ' is already in a group.\r\n');
        return;
    }
    var existingInvite = tapestry.world.getProperty(target.id, 'group_invite_from');
    if (existingInvite) {
        player.send(target.name + ' already has a pending invitation.\r\n');
        return;
    }
    tapestry.world.setProperty(target.id, 'group_invite_from', player.entityId);
    tapestry.world.setProperty(target.id, 'group_invite_expires', Date.now() + 60000);
    player.send('You invite ' + target.name + ' to join your group.\r\n');
    tapestry.world.send(target.id,
        player.name + ' invites you to join their group. Type \'group accept\' to join.\r\n');
}

function handleGroupAccept(player) {
    var inviterId = tapestry.world.getProperty(player.entityId, 'group_invite_from');
    if (!inviterId) {
        player.send('You have no pending group invitation.\r\n');
        return;
    }
    var expires = tapestry.world.getProperty(player.entityId, 'group_invite_expires') || 0;
    tapestry.world.setProperty(player.entityId, 'group_invite_from', null);
    tapestry.world.setProperty(player.entityId, 'group_invite_expires', null);

    if (Date.now() > expires) {
        player.send('That group invitation has expired.\r\n');
        return;
    }

    var inviterName = getPlayerName(inviterId);
    if (!inviterName) {
        player.send('That player is no longer online.\r\n');
        return;
    }

    var groupId = getGroupId(inviterId);
    var isNewGroup = !groupId;
    if (isNewGroup) {
        groupId = generateGroupId();
        addToGroup(inviterId, inviterId, groupId);
    }
    addToGroup(player.entityId, inviterId, groupId);

    player.send('You join ' + inviterName + '\'s group.\r\n');
    tapestry.world.send(inviterId, player.name + ' joins your group.\r\n');
    sendToGroupExcept(player.entityId, inviterId, player.name + ' joins the group.\r\n');

    if (isNewGroup) {
        tapestry.events.publish('group.created', { leaderId: inviterId, groupId: groupId });
    }
    tapestry.events.publish('group.member.joined', {
        memberId: player.entityId, leaderId: inviterId, groupId: groupId
    });
}

function handleGroupDecline(player) {
    var inviterId = tapestry.world.getProperty(player.entityId, 'group_invite_from');
    if (!inviterId) {
        player.send('You have no pending group invitation.\r\n');
        return;
    }
    tapestry.world.setProperty(player.entityId, 'group_invite_from', null);
    tapestry.world.setProperty(player.entityId, 'group_invite_expires', null);
    player.send('You decline the group invitation.\r\n');
    var inviterName = getPlayerName(inviterId);
    if (inviterName) {
        tapestry.world.send(inviterId, player.name + ' declines your group invitation.\r\n');
    }
}

function handleGroupLeave(player) {
    if (!isInGroup(player.entityId)) {
        player.send('You are not in a group.\r\n');
        return;
    }
    var members = getGroupMembers(player.entityId);
    var remaining = [];
    for (var i = 0; i < members.length; i++) {
        if (members[i] !== player.entityId) { remaining.push(members[i]); }
    }

    var groupId = getGroupId(player.entityId);
    var wasLeader = isGroupLeader(player.entityId);
    removeFromGroup(player.entityId);
    player.send('You leave the group.\r\n');
    tapestry.events.publish('group.member.left', {
        memberId: player.entityId, groupId: groupId, reason: 'leave'
    });

    if (remaining.length === 0) { return; }

    if (wasLeader) {
        var newLeaderId = promoteNextLeader(player.entityId, remaining);
        if (newLeaderId) {
            var newLeaderName = getPlayerName(newLeaderId);
            for (var j = 0; j < remaining.length; j++) {
                tapestry.world.send(remaining[j],
                    player.name + ' leaves the group. ' + newLeaderName + ' is now the group leader.\r\n');
            }
            tapestry.events.publish('group.member.promoted', {
                memberId: newLeaderId, oldLeaderId: player.entityId, groupId: groupId
            });
        } else {
            for (var k = 0; k < remaining.length; k++) {
                removeFromGroup(remaining[k]);
                tapestry.world.send(remaining[k], 'The group has been disbanded.\r\n');
            }
            tapestry.events.publish('group.disbanded', { groupId: groupId });
        }
    } else {
        for (var m = 0; m < remaining.length; m++) {
            tapestry.world.send(remaining[m], player.name + ' leaves the group.\r\n');
        }
    }
}

function handleGroupKick(player, args) {
    if (!isInGroup(player.entityId)) {
        player.send('You are not in a group.\r\n');
        return;
    }
    if (!isGroupLeader(player.entityId)) {
        player.send('Only the group leader can kick members.\r\n');
        return;
    }
    if (args.length === 0) {
        player.send('Kick whom?\r\n');
        return;
    }
    var targetName = args[0];
    var target = tapestry.world.findPlayerByName(targetName);
    if (!target) {
        player.send(targetName + ' is not online.\r\n');
        return;
    }
    if (target.id === player.entityId) {
        player.send('You cannot kick yourself. Use \'group disband\' or \'group leave\'.\r\n');
        return;
    }
    if (getGroupId(target.id) !== getGroupId(player.entityId)) {
        player.send(target.name + ' is not in your group.\r\n');
        return;
    }
    var groupId = getGroupId(target.id);
    removeFromGroup(target.id);
    tapestry.world.send(target.id, 'You have been removed from the group.\r\n');
    player.send('You remove ' + target.name + ' from the group.\r\n');
    var members = getGroupMembers(player.entityId);
    for (var i = 0; i < members.length; i++) {
        tapestry.world.send(members[i], target.name + ' has been removed from the group.\r\n');
    }
    tapestry.events.publish('group.member.kicked', {
        memberId: target.id, kickerId: player.entityId, groupId: groupId
    });
}

function handleGroupPromote(player, args) {
    if (!isInGroup(player.entityId)) {
        player.send('You are not in a group.\r\n');
        return;
    }
    if (!isGroupLeader(player.entityId)) {
        player.send('Only the group leader can promote members.\r\n');
        return;
    }
    if (args.length === 0) {
        player.send('Promote whom?\r\n');
        return;
    }
    var targetName = args[0];
    var target = tapestry.world.findPlayerByName(targetName);
    if (!target) {
        player.send(targetName + ' is not online.\r\n');
        return;
    }
    if (getGroupId(target.id) !== getGroupId(player.entityId)) {
        player.send(target.name + ' is not in your group.\r\n');
        return;
    }
    var groupId = getGroupId(player.entityId);
    var members = getGroupMembers(player.entityId);
    for (var i = 0; i < members.length; i++) {
        tapestry.world.setProperty(members[i], 'group_leader', target.id);
    }
    player.send(target.name + ' is now the group leader.\r\n');
    tapestry.world.send(target.id, 'You are now the group leader.\r\n');
    for (var j = 0; j < members.length; j++) {
        if (members[j] !== player.entityId && members[j] !== target.id) {
            tapestry.world.send(members[j], target.name + ' is now the group leader.\r\n');
        }
    }
    tapestry.events.publish('group.member.promoted', {
        memberId: target.id, oldLeaderId: player.entityId, groupId: groupId
    });
}

function handleGroupDisband(player) {
    if (!isInGroup(player.entityId)) {
        player.send('You are not in a group.\r\n');
        return;
    }
    if (!isGroupLeader(player.entityId)) {
        player.send('Only the group leader can disband the group.\r\n');
        return;
    }
    var groupId = getGroupId(player.entityId);
    var members = getGroupMembers(player.entityId);
    for (var i = 0; i < members.length; i++) {
        removeFromGroup(members[i]);
        if (members[i] !== player.entityId) {
            tapestry.world.send(members[i], 'The group has been disbanded.\r\n');
        }
    }
    player.send('You disband the group.\r\n');
    tapestry.events.publish('group.disbanded', { leaderId: player.entityId, groupId: groupId });
}

function handleGroupList(player) {
    if (!isInGroup(player.entityId)) {
        player.send('You are not in a group.\r\n');
        return;
    }
    var members = getGroupMembers(player.entityId);
    var leaderId = getGroupLeaderId(player.entityId);
    var playerRoom = tapestry.world.getEntityRoomId(player.entityId);
    var rows = [];
    for (var i = 0; i < members.length; i++) {
        var memberId = members[i];
        var memberEntity = tapestry.world.getEntity(memberId);
        if (!memberEntity) { continue; }
        var level = tapestry.progression.getLevel(memberId, 'combat');
        var hp = memberEntity.stats.hp;
        var maxHp = memberEntity.stats.max_hp;
        var memberRoom = tapestry.world.getEntityRoomId(memberId);
        var loc = (memberRoom === playerRoom) ? 'here' : 'elsewhere';
        var nameLabel = memberEntity.name;
        if (memberId === leaderId) { nameLabel = nameLabel + ' (leader)'; }
        var line = padRight(nameLabel, 20) + 'Lv' + padLeft(String(level), 2)
            + '  HP ' + padLeft(String(hp), 4) + '/' + padLeft(String(maxHp), 4)
            + '  [' + loc + ']';
        rows.push({ type: 'text', content: '  ' + line });
    }
    var output = tapestry.ui.panel({
        sections: [
            { rows: [{ type: 'title', left: 'Group', right: members.length + ' members' }] },
            { separatorAbove: 'minor', rows: rows }
        ]
    });
    player.send('\r\n' + output + '\r\n');
}

tapestry.commands.register({
    name: 'gtell',
    aliases: ['gt'],
    description: 'Send a message to your group',
    category: 'communication',
    handler: function(player, args) {
        if (!isInGroup(player.entityId)) {
            player.send('You are not in a group.\r\n');
            return;
        }
        var message = args.join(' ');
        if (!message) {
            player.send('Group tell what?\r\n');
            return;
        }
        var formatted = '<group>[Group] ' + player.name + ': "' + message + '"</group>\r\n';
        sendToGroup(player.entityId, formatted);
    }
});

function padRight(str, len) {
    while (str.length < len) { str = str + ' '; }
    return str.substring(0, len);
}

function padLeft(str, len) {
    while (str.length < len) { str = ' ' + str; }
    return str;
}

tapestry.events.on('combat.kill', function(event) {
    var killerId = event.sourceEntityId;
    var victimId = event.targetEntityId;

    if (!killerId || !victimId) { return; }
    if (!isInGroup(killerId)) { return; }

    var rawGold = tapestry.world.getProperty(victimId, 'gold');
    var gold = rawGold ? parseInt(rawGold, 10) : 0;
    if (!gold || gold <= 0) { return; }

    var groupMembers = getSameRoomGroupMembers(killerId);
    var recipients = [killerId];
    for (var i = 0; i < groupMembers.length; i++) {
        recipients.push(groupMembers[i]);
    }

    if (recipients.length <= 1) { return; }

    var share = Math.floor(gold / recipients.length);
    if (share <= 0) { return; }

    var remainder = gold - (share * recipients.length);

    tapestry.world.setProperty(victimId, 'gold', 0);

    for (var k = 0; k < recipients.length; k++) {
        var amount = share;
        if (recipients[k] === killerId) { amount = share + remainder; }
        tapestry.currency.addGold(recipients[k], amount, 'group:split');
        tapestry.world.send(recipients[k],
            amount + ' gold coins are divided among the group.\r\n');
    }
});
