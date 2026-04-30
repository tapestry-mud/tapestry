// packs/tapestry-core/scripts/classes/mage.js
tapestry.classes.register({
    id: "mage",
    name: "Mage",
    tagline: "Student of arcane forces",
    description: "Mages wield raw magical power to heal allies and destroy enemies. Strong resource growth and a broad spell list make them versatile, but their HP is fragile.",
    track: "magic",
    starting_alignment: 0,
    level_up_flavor: "Your understanding of magic deepens.",
    allowed_categories: ["human"],
    allowed_genders: ["male", "female", "other"],
    trains_per_level: 5,
    growth_bonuses: {
        max_resource: "intelligence",
        max_hp: "constitution"
    },
    stat_growth: {
        max_hp: "1d6",
        max_resource: "2d4+1"
    },
    path: [
        { level: 1,  ability_id: "cure_light" },
        { level: 3,  ability_id: "fireball" },
        { level: 5,  ability_id: "shield" },
        { level: 10, ability_id: "blindness" },
        { level: 15, ability_id: "poison" },
        { level: 20, ability_id: "second_cast" }
    ]
});
