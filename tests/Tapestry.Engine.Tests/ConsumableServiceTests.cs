using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Consumables;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests;

public class ConsumableServiceTests
{
    [Fact]
    public void Consume_DoesNotModifySustenance_KernelDoesNotOwnNutrition()
    {
        // After the survival extraction, the kernel must know nothing about hunger.
        // ConsumableService still emits sustenanceValue in the item.consumed event,
        // but it must NOT apply it — the @tapestry/survival pack owns that.
        var world = new World();
        var eventBus = new EventBus();
        var service = new ConsumableService(world, eventBus);

        var player = new Entity("player", "Eater");
        world.TrackEntity(player);
        player.SetProperty("sustenance", 50);

        var food = new Entity("item", "Bread");
        food.SetProperty(ConsumableProperties.ConsumeMethod, "eat");
        food.SetProperty(ConsumableProperties.SustenanceValue, 30);
        world.TrackEntity(food);
        player.AddToContents(food);

        var result = service.Consume(player.Id, food.Id);

        result.Success.Should().BeTrue();
        var sustenance = player.TryGetProperty<int>("sustenance", out var s) ? s : -1;
        sustenance.Should().Be(50); // unchanged: kernel no longer applies nutrition
    }

    [Fact]
    public void Consume_StillEmitsSustenanceValue_OnItemConsumedEvent()
    {
        // The pack relies on sustenanceValue being present in the item.consumed
        // event so its subscriber can apply nutrition. Guard that contract.
        var world = new World();
        var eventBus = new EventBus();
        var service = new ConsumableService(world, eventBus);

        var player = new Entity("player", "Eater");
        world.TrackEntity(player);

        var food = new Entity("item", "Bread");
        food.SetProperty(ConsumableProperties.ConsumeMethod, "eat");
        food.SetProperty(ConsumableProperties.SustenanceValue, 30);
        world.TrackEntity(food);
        player.AddToContents(food);

        object? emitted = null;
        eventBus.Subscribe("item.consumed", evt => emitted = evt.Data.GetValueOrDefault("sustenanceValue"));

        service.Consume(player.Id, food.Id);

        Convert.ToInt32(emitted).Should().Be(30);
    }
}
