using FluentAssertions;
using System.Text.Json;
using Tapestry.Engine;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class NotificationHandlerTests
{
    [Fact]
    public void DrainAndSend_SendsNotificationShow_ForEachNotification()
    {
        var cm = new FakeGmcpConnectionManager();
        var handler = new NotificationHandler(cm);
        var entityId = Guid.NewGuid();

        var notifications = new List<Notification>
        {
            new("quest_complete", 50, "Quest done!\r\n",
                "Notification.Show",
                new { type = "quest_complete", title = "Kill Quest", body = "100 XP", priority = 50 }),
        };

        handler.DrainAndSend(entityId, notifications);

        cm.Sent.Should().ContainSingle(x => x.Package == "Notification.Show");
        var sent = cm.Sent.First(x => x.Package == "Notification.Show");
        var json = JsonSerializer.Serialize(sent.Payload);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("type").GetString().Should().Be("quest_complete");
        doc.RootElement.GetProperty("title").GetString().Should().Be("Kill Quest");
    }

    [Fact]
    public void DrainAndSend_SkipsNotifications_WithoutGmcpData()
    {
        var cm = new FakeGmcpConnectionManager();
        var handler = new NotificationHandler(cm);
        var entityId = Guid.NewGuid();

        var notifications = new List<Notification>
        {
            new("quest_progress", 50, "[Quest] Kill trollocs [2/5]\r\n"),
        };

        handler.DrainAndSend(entityId, notifications);

        cm.Sent.Should().BeEmpty();
    }
}
