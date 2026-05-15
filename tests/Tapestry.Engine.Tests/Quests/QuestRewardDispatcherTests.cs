using FluentAssertions;
using Tapestry.Engine;
using Tapestry.Engine.Quests;

namespace Tapestry.Engine.Tests.Quests;

public class QuestRewardDispatcherTests
{
    private static Entity MakePlayer()
    {
        return new Entity("player", "TestPlayer");
    }

    private static QuestRewardDispatcher MakeDispatcher(
        FakeProgressionService? progression = null,
        FakeCurrencyService? currency = null,
        FakeProficiencyService? proficiency = null,
        FakeItemRegistry? items = null,
        FakeInventoryService? inventory = null)
    {
        return new QuestRewardDispatcher(
            progression ?? new FakeProgressionService(),
            currency ?? new FakeCurrencyService(),
            proficiency ?? new FakeProficiencyService(),
            items ?? new FakeItemRegistry(),
            inventory ?? new FakeInventoryService());
    }

    [Fact]
    public void Dispatch_GrantsXp_WhenRewardHasXp()
    {
        var progression = new FakeProgressionService();
        var dispatcher = MakeDispatcher(progression: progression);
        var player = MakePlayer();

        dispatcher.Dispatch(player, new QuestReward { Xp = 250 });

        progression.Calls.Should().ContainSingle(c =>
            c.EntityId == player.Id && c.Amount == 250 && c.TrackName == "main" && c.Source == "quest");
    }

    [Fact]
    public void Dispatch_SkipsXpGrant_WhenXpIsZero()
    {
        var progression = new FakeProgressionService();
        var dispatcher = MakeDispatcher(progression: progression);

        dispatcher.Dispatch(MakePlayer(), new QuestReward { Xp = 0 });

        progression.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Dispatch_AddsGold_WhenRewardHasGold()
    {
        var currency = new FakeCurrencyService();
        var dispatcher = MakeDispatcher(currency: currency);
        var player = MakePlayer();

        dispatcher.Dispatch(player, new QuestReward { Gold = 50 });

        currency.Calls.Should().ContainSingle(c =>
            c.Entity == player && c.Delta == 50 && c.Reason == "quest_reward");
    }

    [Fact]
    public void Dispatch_SkipsGold_WhenGoldIsZero()
    {
        var currency = new FakeCurrencyService();
        var dispatcher = MakeDispatcher(currency: currency);

        dispatcher.Dispatch(MakePlayer(), new QuestReward { Gold = 0 });

        currency.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Dispatch_TeachesEachAbility()
    {
        var proficiency = new FakeProficiencyService();
        var dispatcher = MakeDispatcher(proficiency: proficiency);
        var player = MakePlayer();

        dispatcher.Dispatch(player, new QuestReward { Abilities = ["dodge", "parry"] });

        proficiency.Calls.Should().HaveCount(2);
        proficiency.Calls.Should().Contain(c => c.EntityId == player.Id && c.AbilityId == "dodge" && c.InitialProficiency == 1);
        proficiency.Calls.Should().Contain(c => c.EntityId == player.Id && c.AbilityId == "parry" && c.InitialProficiency == 1);
    }

    [Fact]
    public void Dispatch_SetsClassProperty_WhenClassUnlockPresent()
    {
        var dispatcher = MakeDispatcher();
        var player = MakePlayer();

        dispatcher.Dispatch(player, new QuestReward { ClassUnlock = "warrior" });

        player.GetProperty<string>("class").Should().Be("warrior");
    }

    [Fact]
    public void Dispatch_SkipsClassProperty_WhenClassUnlockNull()
    {
        var dispatcher = MakeDispatcher();
        var player = MakePlayer();

        dispatcher.Dispatch(player, new QuestReward { ClassUnlock = null });

        player.GetProperty<string>("class").Should().BeNull();
    }

    [Fact]
    public void Dispatch_SetsRaceProperty_WhenRaceUnlockPresent()
    {
        var dispatcher = MakeDispatcher();
        var player = MakePlayer();

        dispatcher.Dispatch(player, new QuestReward { RaceUnlock = "elf" });

        player.GetProperty<string>("race").Should().Be("elf");
    }

    [Fact]
    public void Dispatch_SkipsRaceProperty_WhenRaceUnlockNull()
    {
        var dispatcher = MakeDispatcher();
        var player = MakePlayer();

        dispatcher.Dispatch(player, new QuestReward { RaceUnlock = null });

        player.GetProperty<string>("race").Should().BeNull();
    }

    [Fact]
    public void Dispatch_SpawnsAndPicksUpItem_WhenTemplateFound()
    {
        var fakeItem = new Entity("item", "Sword of Quest");
        var items = new FakeItemRegistry { FakeItem = fakeItem };
        var inventory = new FakeInventoryService();
        var dispatcher = MakeDispatcher(items: items, inventory: inventory);
        var player = MakePlayer();

        dispatcher.Dispatch(player, new QuestReward { Items = ["lf:quest-sword"] });

        items.QueriedTemplateIds.Should().Contain("lf:quest-sword");
        inventory.Calls.Should().ContainSingle(c =>
            c.Entity == player && c.Item == fakeItem && c.Silent);
    }

    [Fact]
    public void Dispatch_SkipsItem_WhenTemplateNotFound_NoThrow()
    {
        var items = new FakeItemRegistry { FakeItem = null }; // always returns null
        var inventory = new FakeInventoryService();
        var dispatcher = MakeDispatcher(items: items, inventory: inventory);
        var player = MakePlayer();

        var act = () => dispatcher.Dispatch(player, new QuestReward { Items = ["lf:missing-item"] });

        act.Should().NotThrow();
        inventory.Calls.Should().BeEmpty();
    }

    // ---- Manual fakes ----

    private sealed class FakeProgressionService : IQuestProgressionService
    {
        public record XpCall(Guid EntityId, int Amount, string TrackName, string Source);
        public List<XpCall> Calls { get; } = new();

        public void GrantExperience(Guid entityId, int amount, string trackName, string source)
        {
            Calls.Add(new XpCall(entityId, amount, trackName, source));
        }
    }

    private sealed class FakeCurrencyService : IQuestCurrencyService
    {
        public record GoldCall(Entity Entity, int Delta, string Reason);
        public List<GoldCall> Calls { get; } = new();

        public int AddGold(Entity entity, int delta, string reason)
        {
            Calls.Add(new GoldCall(entity, delta, reason));
            return delta;
        }
    }

    private sealed class FakeProficiencyService : IQuestProficiencyService
    {
        public record LearnCall(Guid EntityId, string AbilityId, int InitialProficiency);
        public List<LearnCall> Calls { get; } = new();

        public void Learn(Guid entityId, string abilityId, int initialProficiency = 1)
        {
            Calls.Add(new LearnCall(entityId, abilityId, initialProficiency));
        }
    }

    private sealed class FakeItemRegistry : IQuestItemRegistry
    {
        public Entity? FakeItem { get; set; }
        public List<string> QueriedTemplateIds { get; } = new();

        public Entity? CreateItem(string templateId)
        {
            QueriedTemplateIds.Add(templateId);
            return FakeItem;
        }
    }

    private sealed class FakeInventoryService : IQuestInventoryService
    {
        public record PickUpCall(Entity Entity, Entity Item, bool Silent);
        public List<PickUpCall> Calls { get; } = new();

        public bool PickUp(Entity entity, Entity item, bool silent = false)
        {
            Calls.Add(new PickUpCall(entity, item, silent));
            return true;
        }
    }
}
