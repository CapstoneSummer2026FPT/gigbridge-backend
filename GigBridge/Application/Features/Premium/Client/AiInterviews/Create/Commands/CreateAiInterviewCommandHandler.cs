using Domain.Enums.AiInterviews;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Ai;
using Application.Common.Interfaces.Time;
using Application.Features.Premium.Common.Interfaces;
using Application.Common.Models.Ai;
using Application.Features.Premium.Client.AiInterviews.DTOs;
using Domain.Entities;
using Domain.Enums.Premium;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.AiInterviews.Create.Commands;

public sealed class CreateAiInterviewCommandHandler(
    IApplicationDbContext context,
    IPremiumAccessService premiumAccess,
    IAiServiceClient aiServiceClient,
    IDateTimeService clock) : IRequestHandler<CreateAiInterviewCommand, AiInterviewDefinitionDto>
{
    public async Task<AiInterviewDefinitionDto> Handle(
        CreateAiInterviewCommand command,
        CancellationToken cancellationToken)
    {
        await premiumAccess.RequirePremiumClientAsync(command.UserId, cancellationToken);
        var jobPost = await context.Set<JobPost>().AsNoTracking()
            .Include(x => x.JobPostSkills).ThenInclude(x => x.Skills)
            .FirstOrDefaultAsync(x => x.JobPostsId == command.JobPostId && x.Status == 1 &&
                x.ClientProfiles.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("Job post not found.");
        var skills = jobPost.JobPostSkills.Where(x => x.Skills is not null)
            .Select(x => x.Skills.Name).Concat(jobPost.CustomSkillNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var registered = await aiServiceClient.CreateInterviewDefinitionAsync(
            new AiInterviewDefinitionRequestDto
            {
                JobId = jobPost.JobPostsId.ToString(),
                JobTitle = jobPost.Title,
                JobDescription = jobPost.Description,
                JobSkills = skills,
                Language = command.Request.Language.Trim().ToLowerInvariant(),
                Mode = command.Request.Mode.Trim().ToLowerInvariant(),
                QuestionCount = command.Request.QuestionCount
            },
            cancellationToken);
        var definition = new AiInterviewDefinition
        {
            AiInterviewDefinitionsId = Guid.NewGuid(),
            JobPostId = command.JobPostId,
            ClientUserId = command.UserId,
            Language = registered.Language,
            Mode = registered.Mode,
            QuestionCount = registered.QuestionCount,
            Status = AiInterviewDefinitionStatus.Active,
            ExternalReference = registered.DefinitionReference,
            CreatedAt = clock.UtcNow
        };
        context.Set<AiInterviewDefinition>().Add(definition);
        context.Set<PremiumUsageEvent>().Add(new PremiumUsageEvent
        {
            PremiumUsageEventId = Guid.NewGuid(),
            Type = PremiumUsageEventType.AiInterview,
            UserId = command.UserId,
            JobPostId = command.JobPostId,
            IdempotencyKey = $"ai-interview-definition:{definition.AiInterviewDefinitionsId:N}",
            OccurredAt = definition.CreatedAt,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                definition.Language,
                definition.Mode,
                definition.QuestionCount
            })
        });
        await context.SaveChangesAsync(cancellationToken);
        return new AiInterviewDefinitionDto(
            definition.AiInterviewDefinitionsId,
            definition.JobPostId,
            definition.Language,
            definition.Mode,
            definition.QuestionCount,
            definition.Status.ToString(),
            definition.CreatedAt,
            definition.ExternalReference);
    }
}
