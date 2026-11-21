using System.Text.Json;
using Argus.Contracts.Analytics;
using Argus.Contracts.Health;
using Argus.Contracts.Outbound;

namespace Argus.Contracts.Tests;

public class MessageSerializationTests
{
    // One filled-in example of every message in the catalogue
    public static TheoryData<ArgusMessage> AllMessages() => new()
    {
        new ClientStarted { ClientId = "agora", Version = "1.0.0", Environment = "Development", MachineName = "LAPTOP-1" },
        new ClientStopping { ClientId = "agora", Reason = "Application stopping" },
        new Heartbeat
        {
            ClientId = "agora", DbHealthy = true, DbResponseMs = 3.5, MemoryMb = 182.4, UptimeSec = 3600,
            Requests = 120, Errors = 2, AvgResponseMs = 41.7,
            Gauges = new() { ["reports.pending"] = 4, ["users.online"] = 17 }
        },
        new Heartbeat { ClientId = "demo" }, // a client with no database: DbHealthy stays null
        new ErrorReported { ClientId = "agora", Message = "Object reference not set", ExceptionType = "System.NullReferenceException", Path = "/forums/3" },
        new ActivityRecorded { ClientId = "agora", EventType = "post.liked", Dimension = "Crypto Deals", TargetId = "42", TargetLabel = "Best wallets", ActorId = "u-7" },
        new ActivityRecorded { ClientId = "agora", EventType = "subscription.purchased", Value = 4.99m },
        new HealthStatusChanged { ClientId = "agora", OldStatus = HealthStatus.Healthy, NewStatus = HealthStatus.Down, Reason = "No heartbeat for 30 s", IncidentId = 5 },
        new MaintenanceModeChanged { ClientId = "agora", Enabled = true, Message = "Back at 18:00" },
        new InsightPublished { ClientId = "agora", Title = "Reports spike", Detail = "One forum got 40 % of all reports.", Severity = InsightSeverity.Warning },
    };

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Message_survives_a_json_round_trip(ArgusMessage original)
    {
        var json = JsonSerializer.Serialize(original, original.GetType(), ArgusJson.Options);

        var copy = (ArgusMessage)JsonSerializer.Deserialize(json, original.GetType(), ArgusJson.Options)!;

        // Same type, same values: serializing the copy gives exactly the same JSON
        Assert.IsType(original.GetType(), copy);
        Assert.Equal(original.MessageId, copy.MessageId);
        Assert.Equal(json, JsonSerializer.Serialize(copy, copy.GetType(), ArgusJson.Options));
    }

    [Fact]
    public void Enums_are_sent_as_text()
    {
        var message = new HealthStatusChanged { OldStatus = HealthStatus.Healthy, NewStatus = HealthStatus.Down };

        var json = JsonSerializer.Serialize(message, ArgusJson.Options);

        Assert.Contains("\"newStatus\":\"Down\"", json);
    }

    [Fact]
    public void New_messages_get_their_own_id()
    {
        Assert.NotEqual(new Heartbeat().MessageId, new Heartbeat().MessageId);
    }
}