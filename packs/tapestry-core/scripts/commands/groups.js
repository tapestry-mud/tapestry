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

function sendToGroupExcept(selfId, alsoSkipId, message) {
    var members = getGroupMembers(selfId);
    for (var i = 0; i < members.length; i++) {
        if (members[i] !== selfId && members[i] !== alsoSkipId) {
            tapestry.world.send(members[i], message);
        }
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

function padRight(str, len) {
    while (str.length < len) { str = str + ' '; }
    return str.substring(0, len);
}

function padLeft(str, len) {
    while (str.length < len) { str = ' ' + str; }
    return str;
}

// -- follow --

tapestry.commands.register({
    name: 'follow',
    description: 'Follow a player or stop following. Usage: follow [player] | follow stop',
    category: 'movement',
    roles: ['player'],
    args: {
        target: { type: 'keyword', required: false }
    },
    handler: function(actor, resolved) {
        var target = resolved.target;

        if (!target) {
            actor.send('Follow whom? Usage: follow [player] | follow stop\r\n');
            return;
        }

        if (target.toLowerCase() === 'stop') {
            var leaderId = tapestry.world.getProperty(actor.entityId, 'following');
            if (!leaderId) {
                actor.send('You are not following anyone.\r\n');
                return;
            }
            tapestry.world.setProperty(actor.entityId, 'following', null);
            var leaderName = getPlayerName(leaderId);
            if (leaderName) {
                actor.send('You stop following ' + leaderName + '.\r\n');
                tapestry.world.send(leaderId, actor.name + ' stops following you.\r\n');
            } else {
                actor.send('You stop following them.\r\n');
            }
            tapestry.events.publish('follow.ended', {
                followerId: actor.entityId,
                leaderId: leaderId,
                reason: 'command'
            });
            return;
        }

        if (target.toLowerCase() === actor.name.toLowerCase()) {
            actor.send('You cannot follow yourself.\r\n');
            return;
        }

        var found = tapestry.world.findPlayerByName(target);
        if (!found) {
            actor.send(target + ' is not online.\r\n');
            return;
        }

        if (tapestry.world.getProperty(found.id, 'no_follow')) {
            actor.send(found.name + ' is not accepting followers.\r\n');
            return;
        }

        var currentFollowing = tapestry.world.getProperty(actor.entityId, 'following');
        if (currentFollowing === found.id) {
            actor.send('You are already following ' + found.name + '.\r\n');
            return;
        }

        tapestry.world.setProperty(actor.entityId, 'following', found.id);
        actor.send('You begin following ' + found.name + '.\r\n');
        tapestry.world.send(found.id, actor.name + ' begins following you.\r\n');
        tapestry.events.publish('follow.started', {
            followerId: actor.entityId,
            leaderId: found.id
        });
    }
});

// -- nofollow --

tapestry.commands.register({
    name: 'nofollow',
    description: 'Toggle whether others can follow you. When active, drops current followers.',
    category: 'movement',
    roles: ['player'],
    args: {},
    handler: function(actor, resolved) {
        var current = tapestry.world.getProperty(actor.entityId, 'no_follow');
        if (current) {
            tapestry.world.setProperty(actor.entityId, 'no_follow', null);
            actor.send('You are now accepting followers.\r\n');
        } else {
            tapestry.world.setProperty(actor.entityId, 'no_follow', true);
            actor.send('You are no longer accepting followers.\r\n');
            var online = tapestry.world.getOnlinePlayers();
            for (var i = 0; i < online.length; i++) {
                var followerId = online[i].id;
                if (followerId === actor.entityId) { continue; }
                if (tapestry.world.getProperty(followerId, 'following') === actor.entityId) {
                    tapestry.world.setProperty(followerId, 'following', null);
                    tapestry.world.send(followerId,
                        actor.name + ' is no longer accepting followers.\r\n');
                    tapestry.events.publish('follow.ended', {
                        followerId: followerId,
                        leaderId: actor.entityId,
                        reason: 'nofollow'
                    });
                }
            }
        }
    }
});

// -- follow movement event --

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

// -- group --

tapestry.commands.register({
    name: 'group',
    aliases: ['gr'],
    description: 'Manage your group. Subcommands: invite, accept, decline, leave, kick, promote, disband',
    category: 'progression',
    roles: ['player'],
    args: {
        subcommand: { type: 'keyword', required: false },
        player: { type: 'keyword', required: false }
    },
    handler: function(actor, resolved) {
        var sub = resolved.subcommand ? resolved.subcommand.toLowerCase() : '';
        var playerArg = resolved.player ? [resolved.player] : [];

        if (sub === 'invite') { handleGroupInvite(actor, playerArg); }
        else if (sub === 'accept') { handleGroupAccept(actor); }
        else if (sub === 'decline') { handleGroupDecline(actor); }
        else if (sub === 'leave') { handleGroupLeave(actor); }
        else if (sub === 'kick') { handleGroupKick(actor, playerArg); }
        else if (sub === 'promote') { handleGroupPromote(actor, playerArg); }
        else if (sub === 'disband') { handleGroupDisband(actor); }
        else { handleGroupList(actor); }
    }
});

function handleGroupInvite(actor, args) {
    if (args.length === 0) {
        actor.send('Invite whom?\r\n');
        return;
    }
    var targetName = args[0];
    var target = tapestry.world.findPlayerByName(targetName);
    if (!target) {
        actor.send(targetName + ' is not online.\r\n');
        return;
    }
    if (target.id === actor.entityId) {
        actor.send('You cannot invite yourself.\r\n');
        return;
    }
    if (isInGroup(target.id)) {
        actor.send(target.name + ' is already in a group.\r\n');
        return;
    }
    var existingInvite = tapestry.world.getProperty(target.id, 'group_invite_from');
    if (existingInvite) {
        actor.send(target.name + ' already has a pending invitation.\r\n');
        return;
    }
    tapestry.world.setProperty(target.id, 'group_invite_from', actor.entityId);
    tapestry.world.setProperty(target.id, 'group_invite_expires', Date.now() + 60000);
    actor.send('You invite ' + target.name + ' to join your group.\r\n');
    tapestry.world.send(target.id,
        actor.name + ' invites you to join their group. Type \'group accept\' to join.\r\n');
}

function handleGroupAccept(actor) {
    var inviterId = tapestry.world.getProperty(actor.entityId, 'group_invite_from');
    if (!inviterId) {
        actor.send('You have no pending group invitation.\r\n');
        return;
    }
    var expires = tapestry.world.getProperty(actor.entityId, 'group_invite_expires') || 0;
    tapestry.world.setProperty(actor.entityId, 'group_invite_from', null);
    tapestry.world.setProperty(actor.entityId, 'group_invite_expires', null);

    if (Date.now() > expires) {
        actor.send('That group invitation has expired.\r\n');
        return;
    }

    var inviterName = getPlayerName(inviterId);
    if (!inviterName) {
        actor.send('That player is no longer online.\r\n');
        return;
    }

    var groupId = getGroupId(inviterId);
    var isNewGroup = !groupId;
    if (isNewGroup) {
        groupId = generateGroupId();
        addToGroup(inviterId, inviterId, groupId);
    }
    addToGroup(actor.entityId, inviterId, groupId);

    actor.send('You join ' + inviterName + '\'s group.\r\n');
    tapestry.world.send(inviterId, actor.name + ' joins your group.\r\n');
    sendToGroupExcept(actor.entityId, inviterId, actor.name + ' joins the group.\r\n');

    if (isNewGroup) {
        tapestry.events.publish('group.created', { leaderId: inviterId, groupId: groupId });
    }
    tapestry.events.publish('group.member.joined', {
        memberId: actor.entityId, leaderId: inviterId, groupId: groupId
    });
}

function handleGroupDecline(actor) {
    var inviterId = tapestry.world.getProperty(actor.entityId, 'group_invite_from');
    if (!inviterId) {
        actor.send('You have no pending group invitation.\r\n');
        return;
    }
    tapestry.world.setProperty(actor.entityId, 'group_invite_from', null);
    tapestry.world.setProperty(actor.entityId, 'group_invite_expires', null);
    actor.send('You decline the group invitation.\r\n');
    var inviterName = getPlayerName(inviterId);
    if (inviterName) {
        tapestry.world.send(inviterId, actor.name + ' declines your group invitation.\r\n');
    }
}

function handleGroupLeave(actor) {
    if (!isInGroup(actor.entityId)) {
        actor.send('You are not in a group.\r\n');
        return;
    }
    var members = getGroupMembers(actor.entityId);
    var remaining = [];
    for (var i = 0; i < members.length; i++) {
        if (members[i] !== actor.entityId) { remaining.push(members[i]); }
    }

    var groupId = getGroupId(actor.entityId);
    var wasLeader = isGroupLeader(actor.entityId);
    removeFromGroup(actor.entityId);
    actor.send('You leave the group.\r\n');
    tapestry.events.publish('group.member.left', {
        memberId: actor.entityId, groupId: groupId, reason: 'leave'
    });

    if (remaining.length === 0) { return; }

    if (wasLeader) {
        var newLeaderId = promoteNextLeader(actor.entityId, remaining);
        if (newLeaderId) {
            var newLeaderName = getPlayerName(newLeaderId);
            for (var j = 0; j < remaining.length; j++) {
                tapestry.world.send(remaining[j],
                    actor.name + ' leaves the group. ' + newLeaderName + ' is now the group leader.\r\n');
            }
            tapestry.events.publish('group.member.promoted', {
                memberId: newLeaderId, oldLeaderId: actor.entityId, groupId: groupId
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
            tapestry.world.send(remaining[m], actor.name + ' leaves the group.\r\n');
        }
    }
}

function handleGroupKick(actor, args) {
    if (!isInGroup(actor.entityId)) {
        actor.send('You are not in a group.\r\n');
        return;
    }
    if (!isGroupLeader(actor.entityId)) {
        actor.send('Only the group leader can kick members.\r\n');
        return;
    }
    if (args.length === 0) {
        actor.send('Kick whom?\r\n');
        return;
    }
    var targetName = args[0];
    var target = tapestry.world.findPlayerByName(targetName);
    if (!target) {
        actor.send(targetName + ' is not online.\r\n');
        return;
    }
    if (target.id === actor.entityId) {
        actor.send('You cannot kick yourself. Use \'group disband\' or \'group leave\'.\r\n');
        return;
    }
    if (getGroupId(target.id) !== getGroupId(actor.entityId)) {
        actor.send(target.name + ' is not in your group.\r\n');
        return;
    }
    var groupId = getGroupId(target.id);
    removeFromGroup(target.id);
    tapestry.world.send(target.id, 'You have been removed from the group.\r\n');
    actor.send('You remove ' + target.name + ' from the group.\r\n');
    var members = getGroupMembers(actor.entityId);
    for (var i = 0; i < members.length; i++) {
        tapestry.world.send(members[i], target.name + ' has been removed from the group.\r\n');
    }
    tapestry.events.publish('group.member.kicked', {
        memberId: target.id, kickerId: actor.entityId, groupId: groupId
    });
}

function handleGroupPromote(actor, args) {
    if (!isInGroup(actor.entityId)) {
        actor.send('You are not in a group.\r\n');
        return;
    }
    if (!isGroupLeader(actor.entityId)) {
        actor.send('Only the group leader can promote members.\r\n');
        return;
    }
    if (args.length === 0) {
        actor.send('Promote whom?\r\n');
        return;
    }
    var targetName = args[0];
    var target = tapestry.world.findPlayerByName(targetName);
    if (!target) {
        actor.send(targetName + ' is not online.\r\n');
        return;
    }
    if (getGroupId(target.id) !== getGroupId(actor.entityId)) {
        actor.send(target.name + ' is not in your group.\r\n');
        return;
    }
    var groupId = getGroupId(actor.entityId);
    var members = getGroupMembers(actor.entityId);
    for (var i = 0; i < members.length; i++) {
        tapestry.world.setProperty(members[i], 'group_leader', target.id);
    }
    actor.send(target.name + ' is now the group leader.\r\n');
    tapestry.world.send(target.id, 'You are now the group leader.\r\n');
    for (var j = 0; j < members.length; j++) {
        if (members[j] !== actor.entityId && members[j] !== target.id) {
            tapestry.world.send(members[j], target.name + ' is now the group leader.\r\n');
        }
    }
    tapestry.events.publish('group.member.promoted', {
        memberId: target.id, oldLeaderId: actor.entityId, groupId: groupId
    });
}

function handleGroupDisband(actor) {
    if (!isInGroup(actor.entityId)) {
        actor.send('You are not in a group.\r\n');
        return;
    }
    if (!isGroupLeader(actor.entityId)) {
        actor.send('Only the group leader can disband the group.\r\n');
        return;
    }
    var groupId = getGroupId(actor.entityId);
    var members = getGroupMembers(actor.entityId);
    for (var i = 0; i < members.length; i++) {
        removeFromGroup(members[i]);
        if (members[i] !== actor.entityId) {
            tapestry.world.send(members[i], 'The group has been disbanded.\r\n');
        }
    }
    actor.send('You disband the group.\r\n');
    tapestry.events.publish('group.disbanded', { leaderId: actor.entityId, groupId: groupId });
}

function handleGroupList(actor) {
    if (!isInGroup(actor.entityId)) {
        actor.send('You are not in a group.\r\n');
        return;
    }
    var members = getGroupMembers(actor.entityId);
    var leaderId = getGroupLeaderId(actor.entityId);
    var playerRoom = tapestry.world.getEntityRoomId(actor.entityId);
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
    actor.send('\r\n' + output + '\r\n');
}

// -- gtell --

tapestry.commands.register({
    name: 'gtell',
    aliases: ['gt'],
    description: 'Send a message to your group',
    category: 'communication',
    roles: ['player'],
    args: {
        message: { type: 'text', required: true }
    },
    handler: function(actor, resolved) {
        if (!isInGroup(actor.entityId)) {
            actor.send('You are not in a group.\r\n');
            return;
        }
        var message = resolved.message;
        if (!message) {
            actor.send('Group tell what?\r\n');
            return;
        }
        var formatted = '<group>[Group] ' + actor.name + ': "' + message + '"</group>\r\n';
        sendToGroup(actor.entityId, formatted);
    }
});

// -- gold split on kill --

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
