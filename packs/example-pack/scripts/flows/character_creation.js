// packs/tapestry-core/scripts/flows/character_creation.js

var CORE_GENDER_OPTIONS = [
    {
        label: "Male",
        value: "male",
        tag_line: "He / him",
        description: "NPCs will address you as 'sir' or 'my lord'."
    },
    {
        label: "Female",
        value: "female",
        tag_line: "She / her",
        description: "NPCs will address you as 'ma'am' or 'my lady'."
    },
    {
        label: "Other",
        value: "other",
        tag_line: "They / them",
        description: "NPCs will address you with neutral honorifics."
    }
];

tapestry.flows.register({
    id: "character_creation",
    display_name: "creating character",
    trigger: "new_player_connect",
    wizard_steps: [
        { id: "race",   label: "Race" },
        { id: "gender", label: "Gender" },
        { id: "class",  label: "Class" }
    ],
    steps: [
        {
            id: "welcome",
            type: "info",
            text: "Welcome, traveler. Your story is about to begin."
        },
        {
            id: "race",
            type: "choice",
            prompt: "Choose your race:",
            options: function() {
                return tapestry.races.getAll().map(function(r) {
                    return {
                        label: r.name,
                        value: r.id,
                        tag_line: r.tagline,
                        description: r.description
                    };
                });
            },
            on_select: function(entity, option) {
                tapestry.world.setRace(entity.id, String(option.value));
            }
        },
        {
            id: "gender",
            type: "choice",
            prompt: "Choose your gender:",
            options: CORE_GENDER_OPTIONS,
            on_select: function(entity, option) {
                entity.setProperty("gender", String(option.value));
            }
        },
        {
            id: "class",
            type: "choice",
            prompt: "Choose your class:",
            options: function(entity) {
                var raceId = entity.getProperty("race");
                var gender = entity.getProperty("gender") || "other";
                return tapestry.classes.getEligibleClasses({ race: raceId, gender: gender })
                    .map(function(c) {
                        return {
                            label: c.name,
                            value: c.id,
                            tag_line: c.tagline,
                            description: c.description
                        };
                    });
            },
            on_select: function(entity, option) {
                tapestry.world.setClass(entity.id, String(option.value));
            }
        }
    ]
    // no on_complete -- engine seeds alignment automatically at flow completion
});
