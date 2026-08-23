using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.InternalServices.ESign.Models;
using Domain.Entities;
using Domain.Enums.Chat;
using Domain.Enums.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.InternalServices.ESign.Services;

internal static class ESignDocumentRevision
{
    public const string ChangedEventName = "ESignDocumentRevisionChanged";
    public const string UpsertChangeKind = "upsert";
    public const string DeletedChangeKind = "deleted";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static int Advance(EsignDocument document, DateTime now)
    {
        document.ContentRevision = checked(document.ContentRevision + 1);
        document.UpdatedAt = now;
        return document.ContentRevision;
    }

    public static async Task EnqueueAsync(
        IApplicationDbContext context,
        EsignDocument document,
        DateTime now,
        CancellationToken cancellationToken,
        string changeKind = UpsertChangeKind)
    {
        var recipientUserIds = await ResolveRecipientUserIdsAsync(context, document, cancellationToken);
        var payload = JsonSerializer.Serialize(
            new ESignDocumentRevisionDeliveryPayload(
                document.EsignDocumentsId,
                document.ContractsId,
                document.ContentRevision,
                changeKind),
            JsonOptions);

        foreach (var userId in recipientUserIds)
        {
            context.Set<DeliveryOutbox>().Add(new DeliveryOutbox
            {
                DeliveryOutboxId = Guid.NewGuid(),
                DeliveryKey = $"esign-revision:{document.EsignDocumentsId:D}:{document.ContentRevision}:{userId:D}",
                DeliveryType = (int)DeliveryOutboxType.ESignDocumentRevision,
                RecipientUserId = userId,
                EventSequence = document.ContentRevision,
                Channel = (int)DeliveryChannel.NotificationRealtime,
                Payload = payload,
                Status = (int)DeliveryOutboxStatus.Pending,
                NextAttemptAt = now,
                CreatedAt = now
            });
        }

        ESignTelemetry.RecordRevisionEvent("queued", recipientUserIds.Count);
    }

    private static async Task<IReadOnlyList<Guid>> ResolveRecipientUserIdsAsync(
        IApplicationDbContext context,
        EsignDocument document,
        CancellationToken cancellationToken)
    {
        if (!document.ContractsId.HasValue)
        {
            return await (
                    from jobPost in context.Set<JobPost>().AsNoTracking()
                    join client in context.Set<ClientProfile>().AsNoTracking()
                        on jobPost.ClientProfilesId equals client.ClientProfilesId
                    where jobPost.JobPostsId == document.JobPostsId
                    select client.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        var participantIds = await (
                from contract in context.Set<Contract>().AsNoTracking()
                join client in context.Set<ClientProfile>().AsNoTracking()
                    on contract.ClientProfilesId equals client.ClientProfilesId
                join freelancer in context.Set<FreelancerProfile>().AsNoTracking()
                    on contract.FreelancerProfilesId equals (Guid?)freelancer.FreelancerProfilesId into freelancers
                from freelancer in freelancers.DefaultIfEmpty()
                where contract.ContractsId == document.ContractsId.Value
                select new
                {
                    ClientUserId = client.UserId,
                    FreelancerUserId = freelancer == null ? (Guid?)null : freelancer.UserId
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (participantIds is null)
        {
            return [];
        }

        return participantIds.FreelancerUserId.HasValue
            ? [participantIds.ClientUserId, participantIds.FreelancerUserId.Value]
            : [participantIds.ClientUserId];
    }
}
