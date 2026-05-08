using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tapestry.Engine;
using Tapestry.Scripting;
using Tapestry.Shared;

namespace Tapestry.Scripting.Tests.Modules;

public class InventoryModuleGiveTests
{
    private (JintRuntime rt, World world, SessionManager sessions, CommandRegistry commands) BuildRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTapestryEngine();
        services.AddTapestryScripting();
        var provider = services.BuildServiceProvider();
        var rt = provider.GetRequiredService<JintRuntime>();
        rt.Initialize();
        return (
            rt,
            provider.GetRequiredService<World>(),
            provider.GetRequiredService<SessionManager>(),
            provider.GetRequiredService<CommandRegistry>()
        );
    }

    [Fact]
    public void Give_ReturnsTrue_WhenItemTransferSucceeds()
    {
        var (rt, world, _, _) = BuildRuntime();

        var room = new Room("give-room-1", "Test Room", "A plain room.");
        world.AddRoom(room);

        var giver = new Entity("player", "Rand");
        room.AddEntity(giver);
        world.TrackEntity(giver);

        var receiver = new Entity("player", "Mat");
        room.AddEntity(receiver);
        world.TrackEntity(receiver);

        var sword = new Entity("item:weapon", "a steel sword");
        sword.AddTag("item");
        sword.AddTag("sword");
        giver.AddToContents(sword);
        world.TrackEntity(sword);

        var giverId = giver.Id.ToString();
        var receiverId = receiver.Id.ToString();

        var result = rt.Evaluate($"tapestry.inventory.give('{giverId}', '{receiverId}', 'sword')");

        result.Should().Be(true);
        giver.Contents.Should().NotContain(sword);
        receiver.Contents.Should().Contain(sword);
    }

    [Fact]
    public void Give_ReturnsFalse_WhenItemNotInGiverInventory()
    {
        var (rt, world, _, _) = BuildRuntime();

        var room = new Room("give-room-2", "Test Room 2", "Another plain room.");
        world.AddRoom(room);

        var giver = new Entity("player", "Perrin");
        room.AddEntity(giver);
        world.TrackEntity(giver);

        var receiver = new Entity("player", "Egwene");
        room.AddEntity(receiver);
        world.TrackEntity(receiver);

        var giverId = giver.Id.ToString();
        var receiverId = receiver.Id.ToString();

        var result = rt.Evaluate($"tapestry.inventory.give('{giverId}', '{receiverId}', 'sword')");

        result.Should().Be(false);
        receiver.Contents.Should().BeEmpty();
    }

    [Fact]
    public void GiveCommand_SendsFeedbackAndTransfersItem()
    {
        var (rt, world, sessions, commands) = BuildRuntime();

        var room = new Room("give-cmd-room", "Give Room", "A test room.");
        world.AddRoom(room);

        var giverConn = new FakeConnection();
        var giverEntity = new Entity("player", "Rand");
        giverEntity.AddTag("player");
        room.AddEntity(giverEntity);
        world.TrackEntity(giverEntity);
        sessions.Add(new PlayerSession(giverConn, giverEntity));

        var receiverConn = new FakeConnection();
        var receiverEntity = new Entity("player", "Mat");
        receiverEntity.AddTag("player");
        room.AddEntity(receiverEntity);
        world.TrackEntity(receiverEntity);
        sessions.Add(new PlayerSession(receiverConn, receiverEntity));

        var bystanderConn = new FakeConnection();
        var bystanderEntity = new Entity("player", "Perrin");
        bystanderEntity.AddTag("player");
        room.AddEntity(bystanderEntity);
        world.TrackEntity(bystanderEntity);
        sessions.Add(new PlayerSession(bystanderConn, bystanderEntity));

        var sword = new Entity("item:weapon", "a steel sword");
        sword.AddTag("item");
        sword.AddTag("sword");
        giverEntity.AddToContents(sword);
        world.TrackEntity(sword);

        rt.Execute("""
            tapestry.commands.register({
                name: 'give',
                description: 'Give an item to another player.',
                handler: function(player, args) {
                    if (args.length < 2) {
                        player.send('Give what to whom?\r\n');
                        return;
                    }
                    var keyword = args[0];
                    var targetName = args.slice(1).join(' ');
                    var target = tapestry.inventory.findPlayerInRoom(player.entityId, targetName);
                    if (!target) {
                        player.send("You don't see them here.\r\n");
                        return;
                    }
                    var found = tapestry.inventory.findByKeyword(player.entityId, keyword);
                    if (!found) {
                        player.send("You aren't carrying that.\r\n");
                        return;
                    }
                    var success = tapestry.inventory.give(player.entityId, target.id, keyword);
                    if (success) {
                        player.send('You give ' + found.name + ' to ' + target.name + '.\r\n');
                        tapestry.world.send(target.id, player.name + ' gives you ' + found.name + '.\r\n');
                        player.sendToRoom(player.name + ' gives ' + found.name + ' to ' + target.name + '.\r\n');
                    } else {
                        player.send("You can't give that.\r\n");
                    }
                }
            });
            """, "test-pack");

        var registration = commands.Resolve("give");
        registration.Should().NotBeNull();

        var cmdCtx = new CommandContext
        {
            PlayerEntityId = giverEntity.Id,
            RawInput = "give sword mat",
            Command = "give",
            Args = ["sword", "mat"]
        };

        registration!.Handler(cmdCtx);

        string.Join("", giverConn.SentText).Should().Contain("You give a steel sword to Mat.");
        string.Join("", receiverConn.SentText).Should().Contain("Rand gives you a steel sword.");
        string.Join("", bystanderConn.SentText).Should().Contain("Rand gives a steel sword to Mat.");
        giverEntity.Contents.Should().NotContain(sword);
        receiverEntity.Contents.Should().Contain(sword);
    }
}
