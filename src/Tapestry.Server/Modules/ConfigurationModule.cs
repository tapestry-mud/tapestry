using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Consumables;
using Tapestry.Engine.Containers;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Prompt;
using Tapestry.Engine.Rest;
using Tapestry.Engine.Sustenance;
using Tapestry.Engine.Training;

namespace Tapestry.Server.Modules;

public class ConfigurationModule : IGameModule
{
    private readonly ServerConfig _config;
    private readonly TrainingConfig _trainingConfig;
    private readonly EconomyConfig _economyConfig;
    private readonly PropertyTypeRegistry _propertyRegistry;

    public string Name => "Configuration";

    public ConfigurationModule(
        ServerConfig config,
        TrainingConfig trainingConfig,
        EconomyConfig economyConfig,
        PropertyTypeRegistry propertyRegistry)
    {
        _config = config;
        _trainingConfig = trainingConfig;
        _economyConfig = economyConfig;
        _propertyRegistry = propertyRegistry;
    }

    public void Configure()
    {
        _trainingConfig.Configure(
            _config.Training.RequireSafeRoomForStats,
            _config.Training.TrainableStats,
            _config.Training.CatchUpBoost);

        _economyConfig.Configure(
            _config.Economy.ShopBuyMarkup,
            _config.Economy.ShopSellDiscount);

        CommonProperties.Register(_propertyRegistry);
        CombatProperties.Register(_propertyRegistry);
        InventoryProperties.Register(_propertyRegistry);
        ItemProperties.Register(_propertyRegistry);
        MobProperties.Register(_propertyRegistry);
        ProgressionProperties.Register(_propertyRegistry);
        PromptProperties.Register(_propertyRegistry);
        AbilityProperties.Register(_propertyRegistry);
        TrainingProperties.Register(_propertyRegistry);
        CurrencyProperties.Register(_propertyRegistry);
        SustenanceProperties.Register(_propertyRegistry);
        ConsumableProperties.Register(_propertyRegistry);
        ContainerProperties.Register(_propertyRegistry);
        RestProperties.Register(_propertyRegistry);
    }
}
