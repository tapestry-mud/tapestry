// packs/tapestry-core/scripts/races/human.js
tapestry.races.register({
    id: "human",
    name: "Human",
    tagline: "Adaptable and ambitious",
    description: "Humans are the most common folk in the world. They excel at no single discipline but adapt to any path, making them the most versatile race for any class.",
    race_category: "human",
    starting_alignment: 0,
    cast_cost_modifier: -10,
    stat_caps: {
        strength: 25,
        intelligence: 25,
        wisdom: 25,
        dexterity: 25,
        constitution: 25,
        luck: 25,
        max_hp: 18,
        max_resource: 18,
        max_movement: 16
    },
    racial_flags: []
});
