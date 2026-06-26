using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Common.Models.Ai;
using Application.Features.JobPosts.Client.GenerateJobDescription.Commands;
using Application.Features.JobPosts.Client.GenerateJobDescription.DTOs;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.JobPosts.Client.GenerateJobDescription.Commands;

public class GenerateJobDescriptionCommandHandler
    : IRequestHandler<GenerateJobDescriptionCommand, GenerateJobDescriptionResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IDateTimeService _dateTimeService;

    public GenerateJobDescriptionCommandHandler(
        IApplicationDbContext context,
        IAiServiceClient aiServiceClient,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _aiServiceClient = aiServiceClient;
        _dateTimeService = dateTimeService;
    }

    public async Task<GenerateJobDescriptionResponse> Handle(
        GenerateJobDescriptionCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Fetch active majors
        var dbMajors = await _context.Set<Major>()
            .Where(m => m.IsActive)
            .ToListAsync(cancellationToken);

        var majors = dbMajors
            .Select(m => new MajorOptionDto
            {
                MajorId = m.MajorsId.ToString(),
                Name = m.Name
            })
            .ToList();

        // 2. Fetch active categories with their major relationships
        var dbCategories = await _context.Set<Category>()
            .Include(c => c.MajorCategories)
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        var subcategories = dbCategories
            .SelectMany(c => c.MajorCategories.Select(mc => new CategoryOptionDto
            {
                CategoryId = c.CategoriesId.ToString(),
                MajorId = mc.MajorId.ToString(),
                Name = c.Name
            }))
            .ToList();

        // 3. Fetch active skills
        var dbSkills = await _context.Set<Skill>()
            .Where(s => s.IsActive)
            .ToListAsync(cancellationToken);

        var skills = dbSkills
            .Select(s => new SkillOptionDto
            {
                SkillId = s.SkillsId.ToString(),
                Name = s.Name
            })
            .ToList();

        // 4. Build AI request
        var aiRequest = new JobPostGenerationRequestDto
        {
            ClientPrompt = command.ClientPrompt,
            AllowedMajors = majors,
            AllowedCategories = subcategories,
            AvailableSkills = skills
        };

        // 5. Invoke AI service client
        var aiResponse = await _aiServiceClient.GenerateJobDescriptionAsync(aiRequest, cancellationToken);

        // 6. Map skills (system + custom)
        var finalSkills = new List<GeneratedSkillDto>();
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

        // Add matching system skills
        foreach (var sysSkillIdStr in aiResponse.SystemSkillIds)
        {
            if (Guid.TryParse(sysSkillIdStr, out var sysSkillId))
            {
                var skill = dbSkills.FirstOrDefault(s => s.SkillsId == sysSkillId);
                if (skill != null && finalSkills.All(s => s.SkillsId != skill.SkillsId))
                {
                    finalSkills.Add(new GeneratedSkillDto
                    {
                        SkillsId = skill.SkillsId,
                        Name = skill.Name
                    });
                }
            }
        }

        // Process custom skills without persisting to database
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

        var selectedMajorName = selectedMajorId.HasValue
            ? dbMajors.FirstOrDefault(m => m.MajorsId == selectedMajorId.Value)?.Name
            : null;

        var selectedCategoryName = selectedCategoryId.HasValue
            ? dbCategories.FirstOrDefault(c => c.CategoriesId == selectedCategoryId.Value)?.Name
            : null;

        return new GenerateJobDescriptionResponse
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
            QuestionRecruitment = aiResponse.QuestionRecruitment
        };
    }
}
