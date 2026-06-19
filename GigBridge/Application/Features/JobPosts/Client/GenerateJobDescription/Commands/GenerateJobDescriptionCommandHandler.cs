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
            ClientQuestions = command.VettingQuestions
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Select(q => new ClientQuestionDto { Question = q.Trim() })
                .ToList(),
            AllowedMajors = majors,
            AllowedCategories = subcategories,
            AvailableSkills = skills
        };

        // 5. Invoke AI service client
        var aiResponse = await _aiServiceClient.GenerateJobDescriptionAsync(aiRequest, cancellationToken);

        // 6. Map and register skills (system + custom)
        var finalSkills = new List<GeneratedSkillDto>();
        Guid? selectedCategoryId = null;

        if (Guid.TryParse(aiResponse.CategoryId, out var categoryId))
        {
            selectedCategoryId = categoryId;
        }

        // Add matching system skills
        foreach (var sysSkillIdStr in aiResponse.SystemSkillIds)
        {
            if (Guid.TryParse(sysSkillIdStr, out var sysSkillId))
            {
                var skill = dbSkills.FirstOrDefault(s => s.SkillsId == sysSkillId);
                if (skill != null)
                {
                    finalSkills.Add(new GeneratedSkillDto
                    {
                        SkillsId = skill.SkillsId,
                        Name = skill.Name
                    });
                }
            }
        }

        // Process and register custom skills dynamically
        if (selectedCategoryId.HasValue && aiResponse.CustomSkills != null && aiResponse.CustomSkills.Any())
        {
            var customSkillNames = aiResponse.CustomSkills
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct()
                .ToList();

            foreach (var name in customSkillNames)
            {
                var existingSkill = await _context.Set<Skill>()
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower(), cancellationToken);

                if (existingSkill != null)
                {
                    // Ensure CategorySkill mapping exists
                    var linkExists = await _context.Set<CategorySkill>()
                        .AnyAsync(cs => cs.CategoryId == selectedCategoryId.Value && cs.SkillId == existingSkill.SkillsId, cancellationToken);

                    if (!linkExists)
                    {
                        var categorySkill = new CategorySkill
                        {
                            CategorySkillsId = Guid.NewGuid(),
                            CategoryId = selectedCategoryId.Value,
                            SkillId = existingSkill.SkillsId,
                            CreatedAt = _dateTimeService.UtcNow
                        };
                        _context.Set<CategorySkill>().Add(categorySkill);
                    }

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
                    // Create new system skill
                    var newSkill = new Skill
                    {
                        SkillsId = Guid.NewGuid(),
                        Name = name,
                        IsActive = true,
                        CreatedAt = _dateTimeService.UtcNow
                    };
                    _context.Set<Skill>().Add(newSkill);

                    // Add mapping to current category
                    var categorySkill = new CategorySkill
                    {
                        CategorySkillsId = Guid.NewGuid(),
                        CategoryId = selectedCategoryId.Value,
                        SkillId = newSkill.SkillsId,
                        CreatedAt = _dateTimeService.UtcNow
                    };
                    _context.Set<CategorySkill>().Add(categorySkill);

                    finalSkills.Add(new GeneratedSkillDto
                    {
                        SkillsId = newSkill.SkillsId,
                        Name = newSkill.Name
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        return new GenerateJobDescriptionResponse
        {
            Title = aiResponse.Title,
            CategoryId = selectedCategoryId,
            Skills = finalSkills,
            Description = aiResponse.Description
        };
    }
}
