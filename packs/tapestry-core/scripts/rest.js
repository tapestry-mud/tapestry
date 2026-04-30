tapestry.events.on('entity.rest_state.changed', function(evt) {
    var entityId = evt.data.entityId;
    var newState = evt.data.newState;
    var reason = evt.data.reason;

    if (newState === 'awake' && reason === 'combat') {
        tapestry.world.send(entityId,
            '<alert>You are attacked! You wake up!</alert>\r\n');
    }
});
