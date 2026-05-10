tapestry.commands.register({
    name: 'tags',
    description: 'Inspect and manage tags, keywords, roles, and the tag registry.',
    category: 'admin',
    admin: true,
    handler: function(actor, rawArgs) {
        var sub = (rawArgs && rawArgs.length > 0) ? rawArgs[0].toLowerCase() : '';
        var rest = rawArgs ? rawArgs.slice(1) : [];

        if (sub === 'list') {
            tagsListCmd(actor, rest);
        } else if (sub === 'search') {
            tagsSearchCmd(actor, rest);
        } else if (sub === 'add') {
            tagsAddCmd(actor, rest);
        } else if (sub === 'remove') {
            tagsRemoveCmd(actor, rest);
        } else if (sub === 'registry') {
            tagsRegistryCmd(actor, rest);
        } else if (sub === 'validate') {
            tagsValidateCmd(actor);
        } else {
            actor.send('Usage: tags [list|search|add|remove|registry|validate]\r\n');
            actor.send('  tags list [entity]      - show tags on an entity\r\n');
            actor.send('  tags search [tag]       - find entities with a tag\r\n');
            actor.send('  tags add [entity] [tag] - add a tag to an entity\r\n');
            actor.send('  tags remove [entity] [tag] - remove a tag\r\n');
            actor.send('  tags registry [filter]  - dump the tag registry\r\n');
            actor.send('  tags validate           - check all entities for unregistered tags\r\n');
        }
    }
});

function resolveEntityInRoom(actor, keyword) {
    if (!keyword) { return null; }
    var resolved = tapestry.args.resolve(actor.entityId, keyword, 'visible');
    if (!resolved) { return null; }
    return { id: resolved.id, name: resolved.name };
}

function tagsListCmd(actor, args) {
    if (args.length === 0) {
        actor.send('Usage: tags list [entity]\r\n');
        return;
    }

    var target = resolveEntityInRoom(actor, args[0]);
    if (!target) {
        actor.send("Nothing named '" + args[0] + "' here.\r\n");
        return;
    }

    var e = tapestry.world.getEntity(target.id);
    if (!e) {
        actor.send("Cannot resolve entity.\r\n");
        return;
    }

    var tags = tapestry.world.getEntityTags(target.id);
    var keywords = tapestry.world.getEntityKeywords(target.id);
    var roles = tapestry.world.getEntityRoles(target.id);
    var disposition = tapestry.world.getEntityDisposition(target.id) || 'neutral';
    var type = tapestry.world.getEntityType(target.id) || 'unknown';

    actor.send('[' + e.name + '] (type: ' + type + ')\r\n');
    actor.send('  Tags:        ' + (tags && tags.length ? tags.join(', ') : '(none)') + '\r\n');
    actor.send('  Keywords:    ' + (keywords && keywords.length ? keywords.join(', ') : '(none)') + '\r\n');
    actor.send('  Roles:       ' + (roles && roles.length ? roles.join(', ') : '(none)') + '\r\n');
    actor.send('  Disposition: ' + disposition + '\r\n');
}

function tagsSearchCmd(actor, args) {
    if (args.length === 0) {
        actor.send('Usage: tags search [tag]\r\n');
        return;
    }

    var tag = args[0];
    var entities = tapestry.world.getEntitiesByTag(tag);

    if (!entities || entities.length === 0) {
        actor.send("No entities found with tag '" + tag + "'.\r\n");
        return;
    }

    actor.send("Entities with tag '" + tag + "' (" + entities.length + "):\r\n");
    var limit = Math.min(entities.length, 50);
    for (var i = 0; i < limit; i++) {
        actor.send('  ' + entities[i].name + ' [' + entities[i].type + '] (' + entities[i].id + ')\r\n');
    }
    if (entities.length > 50) {
        actor.send('  ... and ' + (entities.length - 50) + ' more.\r\n');
    }
}

function tagsAddCmd(actor, args) {
    if (args.length < 2) {
        actor.send('Usage: tags add [entity] [tag] [--force]\r\n');
        return;
    }

    var target = resolveEntityInRoom(actor, args[0]);
    if (!target) {
        actor.send("Nothing named '" + args[0] + "' here.\r\n");
        return;
    }

    var tag = args[1];
    var force = false;
    for (var i = 2; i < args.length; i++) {
        if (args[i] === '--force') {
            force = true;
        }
    }

    var templateId = tapestry.world.getEntity(target.id)?.templateId;
    var packContext = null;
    if (templateId && templateId.indexOf(':') > 0) {
        packContext = templateId.substring(0, templateId.indexOf(':'));
    }
    var known = tapestry.world.isTagKnown(tag, packContext);
    if (!known && !force) {
        actor.send("Tag '" + tag + "' is not in the registry. Use --force to add anyway.\r\n");
        return;
    }

    tapestry.world.addTag(target.id, tag);
    var msg = "Added tag '" + tag + "' to " + target.name + '.';
    if (!known) {
        msg += ' (WARNING: unregistered tag)';
    }
    actor.send(msg + '\r\n');
}

function tagsRemoveCmd(actor, args) {
    if (args.length < 2) {
        actor.send('Usage: tags remove [entity] [tag]\r\n');
        return;
    }

    var target = resolveEntityInRoom(actor, args[0]);
    if (!target) {
        actor.send("Nothing named '" + args[0] + "' here.\r\n");
        return;
    }

    var tag = args[1];
    var hadTag = tapestry.world.hasTag(target.id, tag);
    tapestry.world.removeTag(target.id, tag);

    if (hadTag) {
        actor.send("Removed tag '" + tag + "' from " + target.name + '.\r\n');
    } else {
        actor.send(target.name + " did not have tag '" + tag + "'.\r\n");
    }
}

function tagsRegistryCmd(actor, args) {
    var filter = (args.length > 0) ? args[0].toLowerCase() : null;
    var registry = tapestry.world.getTagRegistry();

    if (!registry || registry.length === 0) {
        actor.send('Tag registry is empty.\r\n');
        return;
    }

    // Count by scope
    var engineCount = 0;
    var packCount = 0;
    var filtered = [];

    for (var i = 0; i < registry.length; i++) {
        var entry = registry[i];
        if (entry.isEngine) {
            engineCount++;
        } else {
            packCount++;
        }

        // Apply filter: match against appliesTo types or scope
        if (filter) {
            var matchesType = false;
            for (var j = 0; j < entry.appliesTo.length; j++) {
                if (entry.appliesTo[j].toLowerCase() === filter) {
                    matchesType = true;
                    break;
                }
            }
            if (!matchesType && entry.scope.toLowerCase() !== filter) {
                continue;
            }
        }
        filtered.push(entry);
    }

    // Sort: engine first, then by name
    filtered.sort(function(a, b) {
        if (a.isEngine && !b.isEngine) { return -1; }
        if (!a.isEngine && b.isEngine) { return 1; }
        if (a.name < b.name) { return -1; }
        if (a.name > b.name) { return 1; }
        return 0;
    });

    var title = 'Tag Registry (' + engineCount + ' engine + ' + packCount + ' pack)';
    if (filter) {
        title += ' [filter: ' + filter + ']';
    }
    actor.send(title + ':\r\n');

    for (var k = 0; k < filtered.length; k++) {
        var e = filtered[k];
        var scope = e.isEngine ? 'engine' : e.scope;
        var types = e.appliesTo.join(', ');
        actor.send('  [' + scope + '] ' + e.name + ' - ' + e.description + ' (' + types + ')\r\n');
    }

    if (filtered.length === 0) {
        actor.send('  (no matching entries)\r\n');
    }
}

function tagsValidateCmd(actor) {
    var allEntities = tapestry.world.getAllEntities();
    if (!allEntities || allEntities.length === 0) {
        actor.send('No entities loaded.\r\n');
        return;
    }

    var issues = [];
    for (var i = 0; i < allEntities.length; i++) {
        var e = allEntities[i];
        if (!e.tags) { continue; }
        var packContext = null;
        if (e.templateId && e.templateId.indexOf(':') > 0) {
            packContext = e.templateId.substring(0, e.templateId.indexOf(':'));
        }
        for (var t = 0; t < e.tags.length; t++) {
            var tag = e.tags[t];
            if (!tapestry.world.isTagKnown(tag, packContext)) {
                issues.push({
                    entity: e.name,
                    type: e.type,
                    tag: tag,
                    issue: 'unregistered'
                });
            }
        }
    }

    if (issues.length === 0) {
        actor.send('Validation passed: all tags on loaded entities are registered.\r\n');
        return;
    }

    actor.send('Validation found ' + issues.length + ' issue(s):\r\n');
    var limit = Math.min(issues.length, 100);
    for (var j = 0; j < limit; j++) {
        var iss = issues[j];
        actor.send('  [' + iss.issue + '] ' + iss.entity + ' (' + iss.type + ') has tag: ' + iss.tag + '\r\n');
    }
    if (issues.length > 100) {
        actor.send('  ... and ' + (issues.length - 100) + ' more.\r\n');
    }
}
