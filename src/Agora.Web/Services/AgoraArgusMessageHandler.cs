using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Argus.Client;
using Argus.Contracts;
using Argus.Contracts.Outbound;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Agora.Web.Services;

/// <summary>Scoped, durable recipient for the SDK's Argus inbox messages.</summary>
public sealed class AgoraArgusMessageHandler(
    AgoraDbContext db,
    ArgusOptions options,
    IPublicMaintenanceNotifier maintenanceNotifier,
    ILogger<AgoraArgusMessageHandler> logger)
{
    public async Task HandleMaintenanceModeChangedAsync(MaintenanceModeChanged message)
    {
        ValidateTarget(message);
        // Serializable locks the singleton key/range on SQL Server. That makes a second consumer read the
        // committed, newer state before it can decide whether its event is stale; the message record is saved
        // in the same transaction. The retry also covers a concurrent first-row insert.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var now = DateTimeOffset.UtcNow;
            var shouldBroadcast = false;
            MaintenanceStatus? status = null;
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.ProcessedArgusMessages.AnyAsync(item => item.MessageId == message.MessageId)) return;

            var state = await db.MaintenanceStates.SingleOrDefaultAsync(item => item.Id == MaintenanceState.SingletonId);
            if (state is null)
                throw new InvalidOperationException("The maintenance-state singleton is missing. Apply the InitializeMaintenanceStateSingleton migration before processing Argus maintenance messages.");

            // Record stale messages too, so a RabbitMQ replay remains a no-op. Equal timestamps are stale.
            if (state.LastEventOccurredAtUtc is null || message.OccurredAt > state.LastEventOccurredAtUtc)
            {
                state.IsEnabled = message.Enabled;
                state.Message = NormalizeMaintenanceMessage(message.Message);
                state.LastEventOccurredAtUtc = message.OccurredAt;
                state.UpdatedAtUtc = now;
                shouldBroadcast = true;
                status = new MaintenanceStatus(state.IsEnabled, state.Message, state.UpdatedAtUtc);
            }

            db.ProcessedArgusMessages.Add(new ProcessedArgusMessage { MessageId = message.MessageId, MessageType = nameof(MaintenanceModeChanged), ProcessedAtUtc = now });
            try
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                db.ChangeTracker.Clear();
                continue;
            }

            logger.LogInformation("Processed Argus maintenance event {MessageId}; enabled={Enabled}; changed={Changed}", message.MessageId, message.Enabled, shouldBroadcast);
            if (shouldBroadcast) await maintenanceNotifier.StatusChangedAsync(status!);
            return;
        }
    }

    public async Task HandleHealthStatusChangedAsync(HealthStatusChanged message)
    {
        ValidateTarget(message);
        if (!Enum.IsDefined(message.OldStatus) || !Enum.IsDefined(message.NewStatus))
            throw new InvalidOperationException("Argus health-status message contains an invalid status.");
        if (message.Reason.Length > 1000) throw new InvalidOperationException("Argus health-status reason is too long.");

        db.ArgusHealthStatusEvents.Add(new ArgusHealthStatusEvent
        {
            MessageId = RequireMessageId(message), OldStatus = message.OldStatus, NewStatus = message.NewStatus,
            Reason = message.Reason.Trim(), IncidentId = message.IncidentId,
            OccurredAtUtc = message.OccurredAt, ReceivedAtUtc = DateTimeOffset.UtcNow
        });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.ArgusHealthStatusEvents.AsNoTracking().AnyAsync(item => item.MessageId == message.MessageId))
            {
                logger.LogInformation("Ignored duplicate Argus health event {MessageId}", message.MessageId);
                return;
            }
            throw;
        }
        logger.LogInformation("Processed Argus health event {MessageId}", message.MessageId);
    }

    public async Task HandleInsightPublishedAsync(InsightPublished message)
    {
        ValidateTarget(message);
        if (string.IsNullOrWhiteSpace(message.Title) || string.IsNullOrWhiteSpace(message.Detail) ||
            message.Title.Length > 300 || message.Detail.Length > 2000 || !Enum.IsDefined(message.Severity))
            throw new InvalidOperationException("Argus insight message is invalid.");
        db.ArgusInsights.Add(new ArgusInsight
        {
            MessageId = RequireMessageId(message), Title = message.Title.Trim(), Detail = message.Detail.Trim(),
            Severity = message.Severity, OccurredAtUtc = message.OccurredAt, ReceivedAtUtc = DateTimeOffset.UtcNow
        });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.ArgusInsights.AsNoTracking().AnyAsync(item => item.MessageId == message.MessageId))
            {
                logger.LogInformation("Ignored duplicate Argus insight {MessageId}", message.MessageId);
                return;
            }
            throw;
        }
        logger.LogInformation("Processed Argus insight {MessageId}", message.MessageId);
    }

    private void ValidateTarget(ArgusMessage message)
    {
        if (message.ClientId != options.ClientId)
        {
            logger.LogWarning("Ignored Argus message {MessageId} for a different client", message.MessageId);
            throw new InvalidOperationException("Argus message targets a different client.");
        }
        RequireMessageId(message);
    }
    private static Guid RequireMessageId(ArgusMessage message) => message.MessageId == Guid.Empty
        ? throw new InvalidOperationException("Argus message has no message ID.") : message.MessageId;
    private static string? NormalizeMaintenanceMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        if (message.Length > 500) throw new InvalidOperationException("Argus maintenance message is too long.");
        return message.Trim();
    }
}
