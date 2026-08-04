using System.Data;
using Application.Common.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence;

internal sealed class DeliveryOutboxStore : IDeliveryOutboxStore
{
    private readonly GigbridgeDbContext _context;

    public DeliveryOutboxStore(GigbridgeDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DeliveryOutboxLease>> ClaimDueAsync(
        DeliveryChannel channel,
        DateTime now,
        DateTime leaseExpiresAt,
        int batchSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH candidates AS (
                SELECT delivery."DeliveryOutboxId"
                FROM "DeliveryOutboxes" AS delivery
                WHERE delivery."Channel" = @channel
                  AND delivery."Status" = @pendingStatus
                  AND delivery."NextAttemptAt" <= @now
                ORDER BY delivery."NextAttemptAt", delivery."DeliveryOutboxId"
                FOR UPDATE SKIP LOCKED
                LIMIT @batchSize
            )
            UPDATE "DeliveryOutboxes" AS delivery
            SET "Status" = @processingStatus,
                "NextAttemptAt" = @leaseExpiresAt,
                "ClaimToken" = gen_random_uuid()
            FROM candidates
            WHERE delivery."DeliveryOutboxId" = candidates."DeliveryOutboxId"
              AND delivery."Channel" = @channel
              AND delivery."Status" = @pendingStatus
              AND delivery."NextAttemptAt" <= @now
            RETURNING delivery."DeliveryOutboxId", delivery."ClaimToken";
            """;

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var transaction = (NpgsqlTransaction?)_context.Database.CurrentTransaction?.GetDbTransaction();
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("channel", NpgsqlDbType.Integer, (int)channel);
            command.Parameters.AddWithValue("pendingStatus", NpgsqlDbType.Integer,
                (int)DeliveryOutboxStatus.Pending);
            command.Parameters.AddWithValue("processingStatus", NpgsqlDbType.Integer,
                (int)DeliveryOutboxStatus.Processing);
            command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            command.Parameters.AddWithValue("leaseExpiresAt", NpgsqlDbType.TimestampTz, leaseExpiresAt);
            command.Parameters.AddWithValue("batchSize", NpgsqlDbType.Integer, batchSize);

            var leases = new List<DeliveryOutboxLease>(batchSize);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                leases.Add(new DeliveryOutboxLease(reader.GetGuid(0), reader.GetGuid(1)));
            }

            return leases;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<int> InsertScheduleStartDeliveriesAsync(
        IReadOnlyCollection<ScheduleStartDeliveryInsert> deliveries,
        CancellationToken cancellationToken)
    {
        if (deliveries.Count == 0)
        {
            return 0;
        }

        const string sql = """
            INSERT INTO "DeliveryOutboxes" (
                "DeliveryOutboxId", "DeliveryKey", "ScheduleId", "RecipientUserId",
                "EventSequence", "Channel", "Payload", "Status", "AttemptCount",
                "NextAttemptAt", "ClaimToken", "CreatedAt")
            SELECT
                @deliveryOutboxId, @deliveryKey, @scheduleId, @recipientUserId,
                @eventSequence, @channel, @payload, @status, 0,
                @nextAttemptAt, NULL, @createdAt
            WHERE EXISTS (
                SELECT 1
                FROM "Schedules" AS schedule
                WHERE schedule."ScheduleId" = @scheduleId
                  AND schedule."Status" = @scheduledStatus
                  AND schedule."AgreementStatus" = @acceptedAgreementStatus
                  AND schedule."Version" = @eventSequence
                  AND schedule."ScheduledAtUtc" = @expectedScheduledAtUtc
                  AND EXISTS (
                      SELECT 1
                      FROM "ConversationParticipants" AS participant
                      WHERE participant."ConversationsId" = schedule."ConversationId"
                        AND participant."UserId" = @recipientUserId
                        AND participant."LeftAt" IS NULL
                        AND participant."DeletedAt" IS NULL
                  )
            )
            ON CONFLICT ("DeliveryKey") DO NOTHING;
            """;

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var transaction = (NpgsqlTransaction?)_context.Database.CurrentTransaction?.GetDbTransaction();
            await using var batch = new NpgsqlBatch(connection, transaction);
            foreach (var candidate in deliveries)
            {
                var delivery = candidate.Delivery;
                var command = new NpgsqlBatchCommand(sql);
                command.Parameters.AddWithValue("deliveryOutboxId", NpgsqlDbType.Uuid,
                    delivery.DeliveryOutboxId);
                command.Parameters.AddWithValue("deliveryKey", NpgsqlDbType.Varchar,
                    delivery.DeliveryKey);
                command.Parameters.AddWithValue("scheduleId", NpgsqlDbType.Uuid,
                    delivery.ScheduleId!.Value);
                command.Parameters.AddWithValue("recipientUserId", NpgsqlDbType.Uuid,
                    delivery.RecipientUserId);
                command.Parameters.AddWithValue("eventSequence", NpgsqlDbType.Integer,
                    delivery.EventSequence);
                command.Parameters.AddWithValue("channel", NpgsqlDbType.Integer,
                    delivery.Channel);
                command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb,
                    delivery.Payload);
                command.Parameters.AddWithValue("status", NpgsqlDbType.Integer,
                    delivery.Status);
                command.Parameters.AddWithValue("nextAttemptAt", NpgsqlDbType.TimestampTz,
                    delivery.NextAttemptAt);
                command.Parameters.AddWithValue("createdAt", NpgsqlDbType.TimestampTz,
                    delivery.CreatedAt);
                command.Parameters.AddWithValue("scheduledStatus", NpgsqlDbType.Integer,
                    (int)ScheduleStatus.Scheduled);
                command.Parameters.AddWithValue("acceptedAgreementStatus", NpgsqlDbType.Integer,
                    (int)ScheduleAgreementStatus.Accepted);
                command.Parameters.AddWithValue("expectedScheduledAtUtc", NpgsqlDbType.TimestampTz,
                    candidate.ExpectedScheduledAtUtc);
                batch.BatchCommands.Add(command);
            }

            return await batch.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IReadOnlyList<ScheduleStartBackfillSchedule>> LoadScheduleStartBackfillPageAsync(
        DateTime windowStartAt,
        DateTime? lastScheduledAtUtc,
        Guid? lastScheduleId,
        int pageSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                schedule."ScheduleId",
                schedule."ConversationId",
                schedule."Title",
                schedule."Details",
                schedule."ScheduledAtUtc",
                schedule."AgreementStatus",
                schedule."Version",
                schedule."MeetingStatus",
                schedule."MeetingJoinUri"
            FROM "Schedules" AS schedule
            WHERE schedule."Status" = @scheduledStatus
              AND schedule."AgreementStatus" = @acceptedAgreementStatus
              AND schedule."ScheduledAtUtc" >= @windowStartAt
              AND (
                  @lastScheduledAtUtc IS NULL
                  OR (schedule."ScheduledAtUtc", schedule."ScheduleId") >
                     (@lastScheduledAtUtc, @lastScheduleId)
              )
            ORDER BY schedule."ScheduledAtUtc", schedule."ScheduleId"
            LIMIT @pageSize;
            """;

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var transaction = (NpgsqlTransaction?)_context.Database.CurrentTransaction?.GetDbTransaction();
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("scheduledStatus", NpgsqlDbType.Integer,
                (int)ScheduleStatus.Scheduled);
            command.Parameters.AddWithValue("acceptedAgreementStatus", NpgsqlDbType.Integer,
                (int)ScheduleAgreementStatus.Accepted);
            command.Parameters.AddWithValue("windowStartAt", NpgsqlDbType.TimestampTz, windowStartAt);
            command.Parameters.Add(new NpgsqlParameter("lastScheduledAtUtc", NpgsqlDbType.TimestampTz)
            {
                Value = lastScheduledAtUtc.HasValue ? lastScheduledAtUtc.Value : DBNull.Value
            });
            command.Parameters.Add(new NpgsqlParameter("lastScheduleId", NpgsqlDbType.Uuid)
            {
                Value = lastScheduleId.HasValue ? lastScheduleId.Value : DBNull.Value
            });
            command.Parameters.AddWithValue("pageSize", NpgsqlDbType.Integer, pageSize);

            var schedules = new List<ScheduleStartBackfillSchedule>(pageSize);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                schedules.Add(new ScheduleStartBackfillSchedule(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetDateTime(4),
                    (ScheduleAgreementStatus)reader.GetInt32(5),
                    reader.GetInt32(6),
                    (MeetingProvisioningStatus)reader.GetInt32(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            }

            return schedules;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
