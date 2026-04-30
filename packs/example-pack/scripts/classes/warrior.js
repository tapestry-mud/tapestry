// packs/tapestry-core/scripts/classes/warrior.js
tapestry.classes.register({
    id: "warrior",
    name: "Warrior",
    tagline: "Master of arms and armor",
    description: "Warriors fight on the front lines, relying on steel and raw strength. Strong HP growth and a deep skill tree reward those who stay in the thick of battle.",
    track: "combat",
    starting_alignment: 0,
    level_up_flavor: "Your martial skill sharpens.",
    allowed_categories: ["human"],
    allowed_genders: ["male", "female", "other"],
    trains_per_level: 5,
    growth_bonuses: {
        max_hp: "constitution",
        max_movement: "dexterity"
    },
    stat_growth: {
        max_hp: "2d6+2",
        max_movement: "1d4"
    },
    path: [
        { level: 1,  ability_id: "dodge" },
        { level: 3,  ability_id: "kick" },
        { level: 5,  ability_id: "parry" },
        { level: 8,  ability_id: "battle_stance" },
        { level: 12, ability_id: "bash" },
        { level: 18, ability_id: "second_attack" },
        { level: 25, ability_id: "enhanced_damage" }
    ]
});
