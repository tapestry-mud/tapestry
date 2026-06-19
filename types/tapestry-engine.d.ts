// Canonical typings for the tapestry.* engine API, surfaced as the module "@tapestry/engine".
// Source of truth: src/Tapestry.Scripting/Modules/*.cs Build() return shapes.
// tapestry-cli vendors a copy (Phase B "tapestry types") and lays it into each pack's types/ dir.
// Keep in sync with the IJintApiModule set and tie to the engine version.
// The index-signature safety net on each namespace keeps the ESM pack migration mechanical:
// any method not grepped here still type-checks as `any` under strict:false/noImplicitAny:false.
declare module "@tapestry/engine" {

  // ---------------------------------------------------------------------------
  // Shared types
  // ---------------------------------------------------------------------------

  export type EntityId = string;

  export interface CommandDef {
    name: string;
    aliases?: string[];
    priority?: number;
    roles?: string[];
    override?: boolean;
    args?: Record<string, any>;
    visibleTo?: (player: any) => boolean;
    handler: (actor: any, resolved: any) => void;
    [key: string]: any;
  }

  export interface BehaviorDef {
    name: string;
    priority?: number;
    override?: boolean;
    handler: (...args: any[]) => any;
    [key: string]: any;
  }

  // ---------------------------------------------------------------------------
  // abilities
  // ---------------------------------------------------------------------------
  export const abilities: {
    forget(...args: any[]): any;
    getDefinition(...args: any[]): any;
    getDisplayName(...args: any[]): any;
    getLearnedAbilities(...args: any[]): any;
    getProficiency(...args: any[]): any;
    increaseProficiency(...args: any[]): any;
    learn(...args: any[]): any;
    queue(...args: any[]): any;
    register(def: any): void;
    search(...args: any[]): any;
    setProficiency(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // admin
  // ---------------------------------------------------------------------------
  export const admin: { [key: string]: any };

  // ---------------------------------------------------------------------------
  // alignment
  // ---------------------------------------------------------------------------
  export const alignment: {
    bucket(...args: any[]): any;
    get(...args: any[]): any;
    history(...args: any[]): any;
    set(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // args
  // ---------------------------------------------------------------------------
  export const args: {
    registerType(...args: any[]): any;
    resolve(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // authoring
  // ---------------------------------------------------------------------------
  export const authoring: {
    clearRoomAttribute(...args: any[]): any;
    createArea(...args: any[]): any;
    createRoom(...args: any[]): any;
    getArea(...args: any[]): any;
    getAreaRooms(...args: any[]): any;
    getAreas(...args: any[]): any;
    recommendEnabled(...args: any[]): any;
    setAreaAttribute(...args: any[]): any;
    setAreaDescription(...args: any[]): any;
    setAreaLore(...args: any[]): any;
    setAreaName(...args: any[]): any;
    setAreaShort(...args: any[]): any;
    setAreaTheme(...args: any[]): any;
    setRoomAttribute(...args: any[]): any;
    setRoomDescription(...args: any[]): any;
    setRoomExit(...args: any[]): any;
    setRoomName(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // classes
  // ---------------------------------------------------------------------------
  export const classes: {
    get(...args: any[]): any;
    getEligibleClasses(...args: any[]): any;
    getPlayerClass(...args: any[]): any;
    register(...args: any[]): any;
    setClass(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // combat
  // ---------------------------------------------------------------------------
  export const combat: {
    applyAC(...args: any[]): any;
    applyDamage(...args: any[]): any;
    engage(...args: any[]): any;
    flee(...args: any[]): any;
    formatDamageVerb(...args: any[]): any;
    getCombatants(...args: any[]): any;
    isInCombat(...args: any[]): any;
    removeFromAllCombat(...args: any[]): any;
    savingThrow(...args: any[]): any;
    setPrimaryTarget(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // commands
  // ---------------------------------------------------------------------------
  export const commands: {
    register(def: CommandDef): void;
    categories(...args: any[]): any;
    listForPlayer(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // config
  // ---------------------------------------------------------------------------
  export const config: {
    get(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // connections
  // ---------------------------------------------------------------------------
  export const connections: {
    create(...args: any[]): any;
    getAll(...args: any[]): any;
    getForRoom(...args: any[]): any;
    remove(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // consumables
  // ---------------------------------------------------------------------------
  export const consumables: {
    consume(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // currency
  // ---------------------------------------------------------------------------
  export const currency: {
    addGold(...args: any[]): any;
    getGold(...args: any[]): any;
    setGold(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // data
  // ---------------------------------------------------------------------------
  export const data: {
    loadYaml(relativePath: string): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // dice
  // ---------------------------------------------------------------------------
  export const dice: {
    roll(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // doors
  // ---------------------------------------------------------------------------
  export const doors: {
    close(...args: any[]): any;
    getDoor(...args: any[]): any;
    hasKey(...args: any[]): any;
    lockDoor(...args: any[]): any;
    open(...args: any[]): any;
    unlock(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // effects
  // ---------------------------------------------------------------------------
  export const effects: {
    apply(entityId: EntityId, effect: any): void;
    getActive(...args: any[]): any;
    hasEffect(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // equipment
  // ---------------------------------------------------------------------------
  export const equipment: {
    equip(...args: any[]): any;
    getEmptyText(...args: any[]): any;
    getSlots(...args: any[]): any;
    setEmptyText(...args: any[]): any;
    transferAll(...args: any[]): any;
    unequip(...args: any[]): any;
    unequipAll(...args: any[]): any;
    unequipAllSilent(...args: any[]): any;
    unequipByKeyword(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // essence
  // ---------------------------------------------------------------------------
  export const essence: {
    format(...args: any[]): any;
    register(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // events
  // ---------------------------------------------------------------------------
  export const events: {
    on(name: string, handler: (evt: any) => void): void;
    publish(name: string, data?: any): void;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // flags
  // ---------------------------------------------------------------------------
  export const flags: {
    playerHasFlag(...args: any[]): any;
    setPlayerFlag(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // flows
  // ---------------------------------------------------------------------------
  export const flows: {
    register(...args: any[]): any;
    trigger(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // gmcp
  // ---------------------------------------------------------------------------
  export const gmcp: {
    send(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // help
  // ---------------------------------------------------------------------------
  export const help: {
    categories(...args: any[]): any;
    list(...args: any[]): any;
    query(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // inventory
  // ---------------------------------------------------------------------------
  export const inventory: {
    destroy(...args: any[]): any;
    drop(...args: any[]): any;
    examineItem(...args: any[]): any;
    fillItem(...args: any[]): any;
    findInRoom(...args: any[]): any;
    getAll(...args: any[]): any;
    getAllFromContainer(...args: any[]): any;
    getContents(...args: any[]): any;
    getFromContainer(...args: any[]): any;
    getItemDetails(...args: any[]): any;
    give(...args: any[]): any;
    pickUp(...args: any[]): any;
    putAllInContainer(...args: any[]): any;
    putInContainer(...args: any[]): any;
    transferAll(...args: any[]): any;
    transferAllSilent(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // items
  // ---------------------------------------------------------------------------
  export const items: {
    spawnToInventory(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // mobs
  // ---------------------------------------------------------------------------
  export const mobs: {
    registerBehavior(...args: any[]): void;
    registerCommand(...args: any[]): void;
    registerScript(...args: any[]): void;
    command(...args: any[]): any;
    getProperties(...args: any[]): any;
    getTicksSinceLastAction(...args: any[]): any;
    invokeHook(...args: any[]): any;
    recordAction(...args: any[]): any;
    spawnMob(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // notifications
  // ---------------------------------------------------------------------------
  export const notifications: {
    enqueue(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // packs
  // Cross-pack sharing is native import/export, NOT packs.*; only introspection survives.
  // ---------------------------------------------------------------------------
  export const packs: {
    list(): any[];
    getAll(): any[];
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // portals
  // ---------------------------------------------------------------------------
  export const portals: {
    getKeywordExits(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // progression
  // ---------------------------------------------------------------------------
  export const progression: {
    calculateMobXp(...args: any[]): any;
    deduct(...args: any[]): any;
    getInfo(...args: any[]): any;
    getLevel(...args: any[]): any;
    getTracks(...args: any[]): any;
    grant(...args: any[]): any;
    groupShare(...args: any[]): any;
    registerTrack(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // quests
  // ---------------------------------------------------------------------------
  export const quests: {
    abandon(...args: any[]): any;
    getState(...args: any[]): any;
    hasQuestMarker(...args: any[]): any;
    offer(...args: any[]): any;
    registerScript(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // races
  // ---------------------------------------------------------------------------
  export const races: {
    getAll(...args: any[]): any;
    getStatCap(...args: any[]): any;
    register(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // rarity
  // ---------------------------------------------------------------------------
  export const rarity: {
    formatInline(...args: any[]): any;
    register(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // registry
  // ---------------------------------------------------------------------------
  export const registry: {
    conflicts(...args: any[]): any;
    list(...args: any[]): any;
    summary(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // respond
  // ---------------------------------------------------------------------------
  export const respond: {
    suppress(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // rest
  // ---------------------------------------------------------------------------
  export const rest: {
    getRestState(...args: any[]): any;
    setRestState(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // returnaddress
  // ---------------------------------------------------------------------------
  export const returnaddress: {
    clear(...args: any[]): any;
    get(...args: any[]): any;
    has(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // rooms
  // ---------------------------------------------------------------------------
  export const rooms: {
    getByPack(...args: any[]): any;
    getEntryPoints(...args: any[]): any;
    getExits(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // schedule
  // ---------------------------------------------------------------------------
  export const schedule: {
    everyForEach(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // shop
  // ---------------------------------------------------------------------------
  export const shop: {
    buy(...args: any[]): any;
    findShopInRoom(...args: any[]): any;
    listings(...args: any[]): any;
    sell(...args: any[]): any;
    value(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // skills
  // ---------------------------------------------------------------------------
  export const skills: {
    advanceSkillTier(...args: any[]): any;
    getProficiency(...args: any[]): any;
    setProficiency(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // stacking
  // ---------------------------------------------------------------------------
  export const stacking: {
    getStacks(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // stats
  // ---------------------------------------------------------------------------
  export const stats: {
    addBaseAttribute(...args: any[]): any;
    addVital(...args: any[]): any;
    get(...args: any[]): any;
    getDisplayName(...args: any[]): any;
    restoreVitals(...args: any[]): any;
    setBase(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // time
  // ---------------------------------------------------------------------------
  export const time: {
    hour(...args: any[]): any;
    period(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // training
  // ---------------------------------------------------------------------------
  export const training: {
    findTrainerInRoom(...args: any[]): any;
    getCap(...args: any[]): any;
    getTrainsAvailable(...args: any[]): any;
    grantTrains(...args: any[]): any;
    practice(...args: any[]): any;
    setCap(...args: any[]): any;
    setTrainable(...args: any[]): any;
    trainStat(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // ui
  // ---------------------------------------------------------------------------
  export const ui: {
    help(...args: any[]): any;
    panel(...args: any[]): any;
    width(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // watch
  // ---------------------------------------------------------------------------
  export const watch: {
    start(...args: any[]): any;
    stop(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // weather
  // ---------------------------------------------------------------------------
  export const weather: {
    current(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // world
  // ---------------------------------------------------------------------------
  export const world: {
    send(entityId: EntityId, text: string): void;
    getProperty(entityId: EntityId, key: string): any;
    setProperty(entityId: EntityId, key: string, value: any): void;
    addRole(...args: any[]): any;
    addTag(...args: any[]): any;
    buildInfo(...args: any[]): any;
    createEntity(...args: any[]): any;
    disconnectPlayer(...args: any[]): any;
    findEntitiesByName(...args: any[]): any;
    findEntityByTag(...args: any[]): any;
    findPlayerByName(...args: any[]): any;
    getAllEntities(...args: any[]): any;
    getCurrentTick(...args: any[]): any;
    getEntitiesByTag(...args: any[]): any;
    getEntitiesInRoom(...args: any[]): any;
    getEntity(...args: any[]): any;
    getEntityDisposition(...args: any[]): any;
    getEntityKeywords(...args: any[]): any;
    getEntityRoles(...args: any[]): any;
    getEntityRoomId(...args: any[]): any;
    getEntityTags(...args: any[]): any;
    getEntityType(...args: any[]): any;
    getExitTarget(...args: any[]): any;
    getItemsInWorld(...args: any[]): any;
    getNpcsInWorld(...args: any[]): any;
    getOnlinePlayers(...args: any[]): any;
    getPropertyRegistry(...args: any[]): any;
    getRoomArea(...args: any[]): any;
    getRoomBiome(...args: any[]): any;
    getRoomDescription(...args: any[]): any;
    getRoomExits(...args: any[]): any;
    getRoomExitsById(...args: any[]): any;
    getRoomName(...args: any[]): any;
    getRoomOccupants(...args: any[]): any;
    getRoomProperties(...args: any[]): any;
    getRoomsInArea(...args: any[]): any;
    getRoomTags(...args: any[]): any;
    getTagRegistry(...args: any[]): any;
    getVisibleEntities(...args: any[]): any;
    hasRole(...args: any[]): any;
    hasTag(...args: any[]): any;
    isTagKnown(...args: any[]): any;
    moveEntity(...args: any[]): any;
    placeEntity(...args: any[]): any;
    purgeEntities(...args: any[]): any;
    removeEntity(...args: any[]): any;
    removeRole(...args: any[]): any;
    removeTag(...args: any[]): any;
    renderAreaMap(...args: any[]): any;
    sameArea(...args: any[]): any;
    searchTemplates(...args: any[]): any;
    sendMotd(...args: any[]): any;
    sendPrivate(...args: any[]): any;
    sendRoomDescription(...args: any[]): any;
    sendToAll(...args: any[]): any;
    sendToRoom(...args: any[]): any;
    sendToRoomExcept(...args: any[]): any;
    sendToRoomExceptMany(...args: any[]): any;
    sendToRoomExceptSleeping(...args: any[]): any;
    setClass(...args: any[]): any;
    setRace(...args: any[]): any;
    teleportEntity(...args: any[]): any;
    triggerDisposition(...args: any[]): any;
    [key: string]: any;
  };

  // ---------------------------------------------------------------------------
  // whoAmI - returns the pack name the calling code lexically belongs to.
  // Attribution is a property of module scope, not a dynamic global.
  // ---------------------------------------------------------------------------
  export function whoAmI(): string;
}
