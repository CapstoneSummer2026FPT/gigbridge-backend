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
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Premium.Client.AiJobPostGenerator.Commands;

public class GenerateJobDescriptionDetailsCommandHandler
    : IRequestHandler<GenerateJobDescriptionDetailsCommand, GenerateJobDescriptionDetailsResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IPremiumAccessService _premiumAccess;

    public GenerateJobDescriptionDetailsCommandHandler(
        IApplicationDbContext context,
        IAiServiceClient aiServiceClient,
        IPremiumAccessService premiumAccess)
    {
        _context = context;
        _aiServiceClient = aiServiceClient;
        _premiumAccess = premiumAccess;
    }

    public async Task<GenerateJobDescriptionDetailsResponse> Handle(
        GenerateJobDescriptionDetailsCommand command,
        CancellationToken cancellationToken)
    {
        await _premiumAccess.RequirePremiumClientAsync(command.UserId, cancellationToken);

        var aiRequest = new JobPostGenerationRequestDto
        {
            ClientPrompt = command.ClientPrompt
        };

        JobPostDetailsGenerationResponseDto aiResponse;
        try
        {
            aiResponse = await _aiServiceClient.GenerateJobDescriptionDetailsAsync(aiRequest, cancellationToken);
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

        return new GenerateJobDescriptionDetailsResponse
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
            BudgetMin = aiResponse.BudgetMin,
            BudgetMax = aiResponse.BudgetMax,
            EstimatedDuration = aiResponse.EstimatedDuration,
            AiDisclaimer = "AI-generated content. Review and edit all fields before publishing."
        };
    }
}
