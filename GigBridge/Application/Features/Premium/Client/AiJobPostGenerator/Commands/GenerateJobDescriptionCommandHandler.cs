using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.Premium.Client.AiJobPostGenerator.Commands;
using Application.Features.Premium.Client.AiJobPostGenerator.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.AiJobPostGenerator.Commands;

public class GenerateJobDescriptionCommandHandler
    : IRequestHandler<GenerateJobDescriptionCommand, GenerateJobDescriptionResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IPremiumAccessService _premiumAccess;

    public GenerateJobDescriptionCommandHandler(
        IApplicationDbContext context,
        IAiServiceClient aiServiceClient,
        IPremiumAccessService premiumAccess)
    {
        _context = context;
        _aiServiceClient = aiServiceClient;
        _premiumAccess = premiumAccess;
    }

    public async Task<GenerateJobDescriptionResponse> Handle(
        GenerateJobDescriptionCommand command,
        CancellationToken cancellationToken)
    {
        await _premiumAccess.RequirePremiumClientAsync(command.UserId, cancellationToken);

        // 1. Build AI request (only containing the prompt)
        var aiRequest = new JobPostGenerationRequestDto
        {
            ClientPrompt = command.ClientPrompt
        };

        // 2. Invoke AI service client
        JobPostGenerationResponseDto aiResponse;
        try
        {
            aiResponse = await _aiServiceClient.GenerateJobDescriptionAsync(aiRequest, cancellationToken);
        }
        catch (Application.Common.Exceptions.ExternalServiceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new Application.Common.Exceptions.ExternalServiceException(
                "AI service is temporarily unavailable. Please try again later.", exception);
        }

        // 3. Map selected Major and Category IDs
        Guid? selectedCategoryId = null;
        Guid? selectedMajorId = null;
        Guid? selectedMajorCategoryId = null;

        if (Guid.TryParse(aiResponse.CategoryId, out var categoryId))
        {
            selectedCategoryId = categoryId;
        }

        if (Guid.TryParse(aiResponse.MajorId, out var majorId))
        {
            selectedMajorId = majorId;
        }

        // 4. Fetch Major and Category names dynamically from the DB
        string? selectedMajorName = null;
        if (selectedMajorId.HasValue)
        {
            var major = await _context.Set<Major>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MajorsId == selectedMajorId.Value, cancellationToken);

            selectedMajorName = major?.Name;
        }

        string? selectedCategoryName = null;
        if (selectedCategoryId.HasValue)
        {
            var category = await _context.Set<Category>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoriesId == selectedCategoryId.Value, cancellationToken);
            selectedCategoryName = category?.Name;
        }

        // 5. Fetch MajorCategory relationship if both are present
        if (selectedCategoryId.HasValue && selectedMajorId.HasValue)
        {
            var majorCategory = await _context.Set<MajorCategory>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    mc => mc.MajorId == selectedMajorId.Value && mc.CategoryId == selectedCategoryId.Value,
                    cancellationToken);

            if (majorCategory != null)
            {
                selectedMajorCategoryId = majorCategory.MajorCategoriesId;
            }
        }

        // 6. Fetch only matching system skills from DB
        var parsedSystemSkillIds = new List<Guid>();
        foreach (var sysSkillIdStr in aiResponse.SystemSkillIds)
        {
            if (Guid.TryParse(sysSkillIdStr, out var sysSkillId))
            {
                parsedSystemSkillIds.Add(sysSkillId);
            }
        }

        var matchedSystemSkills = await _context.Set<Skill>()
            .AsNoTracking()
            .Where(s => parsedSystemSkillIds.Contains(s.SkillsId))
            .ToListAsync(cancellationToken);

        var finalSkills = matchedSystemSkills
            .Select(s => new GeneratedSkillDto
            {
                SkillsId = s.SkillsId,
                Name = s.Name
            })
            .ToList();

        // 7. Process custom skills (checking if they map to existing system skills)
        var finalCustomSkills = new List<string>();

        if (aiResponse.CustomSkills != null)
        {
            var customSkillNames = aiResponse.CustomSkills
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct()
                .ToList();

            foreach (var name in customSkillNames)
            {
                var existingSkill = await _context.Set<Skill>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower(), cancellationToken);

                if (existingSkill != null)
                {
                    if (finalSkills.All(s => s.SkillsId != existingSkill.SkillsId))
                    {
                        finalSkills.Add(new GeneratedSkillDto
                        {
                            SkillsId = existingSkill.SkillsId,
                            Name = existingSkill.Name
                        });
                    }
                }
                else
                {
                    finalCustomSkills.Add(name);
                }
            }
        }

        var response = new GenerateJobDescriptionResponse
        {
            Title = aiResponse.Title,
            MajorId = selectedMajorId,
            MajorName = selectedMajorName,
            CategoryId = selectedCategoryId,
            CategoryName = selectedCategoryName,
            MajorCategoryId = selectedMajorCategoryId,
            Skills = finalSkills,
            CustomSkills = finalCustomSkills,
            Description = aiResponse.Description,
            QuestionRecruitment = aiResponse.QuestionRecruitment,
            BudgetMin = aiResponse.BudgetMin,
            BudgetMax = aiResponse.BudgetMax,
            Currency = aiResponse.Currency,
            AiDisclaimer = "AI-generated content. Review and edit all fields before publishing."
        };
        try
        {
            _context.Set<PremiumUsageEvent>().Add(new PremiumUsageEvent
            {
                PremiumUsageEventId = Guid.NewGuid(),
                Type = PremiumUsageEventType.AiJobGeneration,
                UserId = command.UserId,
                IdempotencyKey = $"ai-job-generation:{Guid.NewGuid():N}",
                OccurredAt = DateTime.UtcNow,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    matchedSkillCount = finalSkills.Count,
                    customSkillCount = finalCustomSkills.Count
                })
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The generated response must not be lost if analytics capture is unavailable.
        }
        return response;
    }
}
