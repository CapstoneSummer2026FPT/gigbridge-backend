using Application.Common.InternalServices.ESign.Models;
namespace Application.Features.Contracts.Milestones.Freelancer.Submit.Common;

/// <summary>
/// Serialized into DeliveryOutbox.Payload for DeliveryOutboxType.MilestoneSubmission rows.
/// Client name/email are snapshotted at enqueue time (matching ContractEsignDeliveryPayload);
/// job/milestone/file details are re-read from the database at dispatch time so the email
/// reflects the latest state if delivery is delayed.
/// </summary>
public sealed record MilestoneSubmissionDeliveryPayload(
    Guid ContractId,
    Guid MilestoneId,
    string ClientEmail,
    string ClientName);
