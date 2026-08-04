using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Mappings;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.Commands;
using Application.Features.Profiles.FreelancerProfile.UpdateFreelancerProfile.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.Profiles.Freelancer;

public class UpdateFreelancerProfileCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesMissingFreelancerProfileAndCalculatesCompletionScore()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            FullName = "Freelancer User",
            Email = "freelancer@example.com",
            Role = (int)UserRole.Freelancer
        };

        context.AddSet(user);
        var profiles = context.AddSet<FreelancerProfile>();
        var (majorId, categoryId) = AddTaxonomy(context);

        var handler = new UpdateFreelancerProfileCommandHandler(
            context,
            new FixedCurrentUserService(userId),
            CreateMapper(),
            NullLogger<UpdateFreelancerProfileCommandHandler>.Instance,
            new FakeMediaService());

        var result = await handler.Handle(new UpdateFreelancerProfileCommand(CreateValidDto(majorId, categoryId)), CancellationToken.None);

        var profile = Assert.Single(profiles.Entities);
        Assert.Equal(profile.FreelancerProfilesId, result.FreelancerProfilesId);
        Assert.Equal(userId, profile.UserId);
        Assert.Equal("Backend Developer", profile.Title);
        Assert.Equal("Experienced .NET developer.", profile.Bio);
        Assert.Equal(0, profile.Availability);
        Assert.Equal("Ho Chi Minh City", profile.Location);
        Assert.Equal(100, profile.ProfileCompletionScore);
        Assert.Equal(majorId, profile.MajorId);
        Assert.Equal(categoryId, Assert.Single(result.Categories).CategoryId);
        Assert.True(user.IsSetup);
        Assert.NotNull(profile.UpdatedAt);
        Assert.Equal(1, context.SaveChangesCount);
    }

    [Fact]
    public async Task Handle_UpdatesExistingFreelancerProfileAndRecalculatesCompletionScore()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            Title = "Old Title",
            Bio = "Old bio",
            Availability = 2,
            Location = "Hanoi",
            ProfileCompletionScore = 40,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var user = new User
        {
            UserId = userId,
            FullName = "Freelancer User",
            Email = "freelancer@example.com",
            Role = (int)UserRole.Freelancer,
            FreelancerProfile = profile
        };

        context.AddSet(user);
        context.AddSet(profile);
        var (majorId, categoryId) = AddTaxonomy(context);

        var handler = new UpdateFreelancerProfileCommandHandler(
            context,
            new FixedCurrentUserService(userId),
            CreateMapper(),
            NullLogger<UpdateFreelancerProfileCommandHandler>.Instance,
            new FakeMediaService());

        var result = await handler.Handle(new UpdateFreelancerProfileCommand(CreateValidDto(majorId, categoryId)), CancellationToken.None);

        Assert.Equal(profile.FreelancerProfilesId, result.FreelancerProfilesId);
        Assert.Equal("Backend Developer", profile.Title);
        Assert.Equal("Experienced .NET developer.", profile.Bio);
        Assert.Equal(0, profile.Availability);
        Assert.Equal("Ho Chi Minh City", profile.Location);
        Assert.Equal(100, profile.ProfileCompletionScore);
        Assert.True(user.IsSetup);
        Assert.Equal(categoryId, Assert.Single(profile.FreelancerProfileCategories).MajorCategory.CategoryId);
    }

    [Fact]
    public async Task Handle_PreservesUnchangedTaxonomySelection()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var (major, category, mapping) = AddTaxonomyMapping(context, "Software Development", "Backend Development");
        var selection = new FreelancerProfileCategory
        {
            FreelancerProfileCategoriesId = Guid.NewGuid(),
            FreelancerProfileId = Guid.NewGuid(),
            MajorCategoryId = mapping.MajorCategoriesId,
            MajorCategory = mapping,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var profile = CreateExistingProfile(userId, major, selection);
        selection.FreelancerProfileId = profile.FreelancerProfilesId;
        selection.FreelancerProfile = profile;
        var user = CreateFreelancerUser(userId, profile);

        context.AddSet(user);
        context.AddSet(profile);
        var selections = context.AddSet(selection);

        var handler = CreateHandler(context, userId);
        await handler.Handle(
            new UpdateFreelancerProfileCommand(CreateValidDto(major.MajorsId, category.CategoriesId)),
            CancellationToken.None);

        var persistedSelection = Assert.Single(profile.FreelancerProfileCategories);
        Assert.Same(selection, persistedSelection);
        Assert.Equal(selection.FreelancerProfileCategoriesId, persistedSelection.FreelancerProfileCategoriesId);
        Assert.Equal(selection.CreatedAt, persistedSelection.CreatedAt);
        Assert.Same(selection, Assert.Single(selections.Entities));
    }

    [Fact]
    public async Task Handle_AddsAndRemovesOnlyChangedTaxonomySelections()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var (major, firstCategory, firstMapping) = AddTaxonomyMapping(context, "Software Development", "Backend Development");
        var (_, retainedCategory, retainedMapping) = AddTaxonomyMapping(context, major, "Frontend Development");
        var (_, addedCategory, _) = AddTaxonomyMapping(context, major, "DevOps");
        var profileId = Guid.NewGuid();
        var removedSelection = CreateSelection(profileId, firstMapping);
        var retainedSelection = CreateSelection(profileId, retainedMapping);
        var profile = CreateExistingProfile(userId, major, removedSelection, retainedSelection);
        var user = CreateFreelancerUser(userId, profile);

        context.AddSet(user);
        context.AddSet(profile);
        var selections = context.AddSet(removedSelection, retainedSelection);

        var handler = CreateHandler(context, userId);
        var dto = CreateValidDto(major.MajorsId, retainedCategory.CategoriesId);
        dto.CategoryIds = new[] { retainedCategory.CategoriesId, addedCategory.CategoriesId };
        await handler.Handle(new UpdateFreelancerProfileCommand(dto), CancellationToken.None);

        Assert.DoesNotContain(profile.FreelancerProfileCategories, item => item.MajorCategoryId == firstMapping.MajorCategoriesId);
        Assert.Contains(profile.FreelancerProfileCategories, item => ReferenceEquals(item, retainedSelection));
        Assert.Contains(profile.FreelancerProfileCategories, item => item.MajorCategory.CategoryId == addedCategory.CategoriesId);
        Assert.DoesNotContain(removedSelection, selections.Entities);
        Assert.Contains(retainedSelection, selections.Entities);
        Assert.Contains(selections.Entities, item => item.MajorCategory.CategoryId == addedCategory.CategoriesId);
        Assert.Equal(100, profile.ProfileCompletionScore);
    }

    [Fact]
    public async Task Handle_ReplacesOldMajorSelectionsWhenMajorChanges()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var (oldMajor, _, oldMapping) = AddTaxonomyMapping(context, "Design", "Graphic Design");
        var (newMajor, newCategory, _) = AddTaxonomyMapping(context, "Software Development", "Backend Development");
        var profileId = Guid.NewGuid();
        var oldSelection = CreateSelection(profileId, oldMapping);
        var profile = CreateExistingProfile(userId, oldMajor, oldSelection);
        var user = CreateFreelancerUser(userId, profile);

        context.AddSet(user);
        context.AddSet(profile);
        context.AddSet(oldSelection);

        var handler = CreateHandler(context, userId);
        var result = await handler.Handle(
            new UpdateFreelancerProfileCommand(CreateValidDto(newMajor.MajorsId, newCategory.CategoriesId)),
            CancellationToken.None);

        Assert.Equal(newMajor.MajorsId, profile.MajorId);
        Assert.DoesNotContain(oldSelection, profile.FreelancerProfileCategories);
        Assert.Equal(newCategory.CategoriesId, Assert.Single(profile.FreelancerProfileCategories).MajorCategory.CategoryId);
        Assert.Equal(newMajor.MajorsId, result.MajorId);
        Assert.Equal(newCategory.CategoriesId, Assert.Single(result.Categories).CategoryId);
    }

    [Fact]
    public async Task Handle_TranslatesDatabaseConcurrencyFailureToConflict()
    {
        var context = new InMemoryApplicationDbContext
        {
            SaveChangesException = new DbUpdateConcurrencyException("Simulated concurrent update.")
        };
        var userId = Guid.NewGuid();
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var user = CreateFreelancerUser(userId, profile);
        context.AddSet(user);
        context.AddSet(profile);
        var (majorId, categoryId) = AddTaxonomy(context);

        var handler = CreateHandler(context, userId);
        var exception = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new UpdateFreelancerProfileCommand(CreateValidDto(majorId, categoryId)),
            CancellationToken.None));

        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);
        Assert.Contains("Reload the latest profile", exception.Message);
    }

    [Fact]
    public async Task Handle_SynchronizesFreelancerSkillsAndReturnsTheirMetadata()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var retainedSkill = new Skill { SkillsId = Guid.NewGuid(), Name = "C#", IsActive = true };
        var removedSkill = new Skill { SkillsId = Guid.NewGuid(), Name = "Legacy Skill", IsActive = true };
        var addedSkill = new Skill { SkillsId = Guid.NewGuid(), Name = "PostgreSQL", IsActive = true };
        var retainedSelection = CreateSkillSelection(profile, retainedSkill);
        var removedSelection = CreateSkillSelection(profile, removedSkill);
        profile.FreelancerSkills.Add(retainedSelection);
        profile.FreelancerSkills.Add(removedSelection);

        context.AddSet(CreateFreelancerUser(userId, profile));
        context.AddSet(profile);
        context.AddSet(retainedSkill, removedSkill, addedSkill);
        var skillSelections = context.AddSet(retainedSelection, removedSelection);
        var (majorId, categoryId) = AddTaxonomy(context);
        var dto = CreateValidDto(majorId, categoryId);
        dto.SkillIds = new[] { retainedSkill.SkillsId, addedSkill.SkillsId };

        var handler = CreateHandler(context, userId);
        var result = await handler.Handle(
            new UpdateFreelancerProfileCommand(dto),
            CancellationToken.None);

        Assert.Contains(profile.FreelancerSkills, item => ReferenceEquals(item, retainedSelection));
        Assert.DoesNotContain(profile.FreelancerSkills, item => item.SkillsId == removedSkill.SkillsId);
        Assert.Contains(profile.FreelancerSkills, item => item.SkillsId == addedSkill.SkillsId);
        Assert.DoesNotContain(removedSelection, skillSelections.Entities);
        Assert.Contains(retainedSelection, skillSelections.Entities);
        Assert.Contains(skillSelections.Entities, item => item.SkillsId == addedSkill.SkillsId);
        Assert.Equal(new[] { "C#", "PostgreSQL" }, result.Skills.Select(skill => skill.SkillName).OrderBy(name => name));
    }

    [Fact]
    public async Task Handle_RejectsInactiveFreelancerSkill()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var inactiveSkill = new Skill { SkillsId = Guid.NewGuid(), Name = "Inactive", IsActive = false };
        context.AddSet(CreateFreelancerUser(userId, profile));
        context.AddSet(profile);
        context.AddSet(inactiveSkill);
        context.AddSet<FreelancerSkill>();
        var (majorId, categoryId) = AddTaxonomy(context);
        var dto = CreateValidDto(majorId, categoryId);
        dto.SkillIds = new[] { inactiveSkill.SkillsId };

        var handler = CreateHandler(context, userId);
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new UpdateFreelancerProfileCommand(dto),
            CancellationToken.None));

        Assert.Contains("active", exception.Message);
    }

    [Fact]
    public async Task Handle_SynchronizesAndReturnsEnrichedPortfolioItems()
    {
        var context = new InMemoryApplicationDbContext();
        var userId = Guid.NewGuid();
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var retainedItem = new PortfolioItem
        {
            PortfolioItemsId = Guid.NewGuid(),
            FreelancerId = profile.FreelancerProfilesId,
            Freelancer = profile,
            Title = "Old title"
        };
        var removedItem = new PortfolioItem
        {
            PortfolioItemsId = Guid.NewGuid(),
            FreelancerId = profile.FreelancerProfilesId,
            Freelancer = profile,
            Title = "Remove me",
            ImageUrl = "https://res.cloudinary.com/gigbridge/image/upload/v1/gigbridge/portfolio/profile/remove.png"
        };
        profile.PortfolioItems.Add(retainedItem);
        profile.PortfolioItems.Add(removedItem);

        context.AddSet(CreateFreelancerUser(userId, profile));
        context.AddSet(profile);
        var portfolioItems = context.AddSet(retainedItem, removedItem);
        var (majorId, categoryId) = AddTaxonomy(context);
        var dto = CreateValidDto(majorId, categoryId);
        dto.PortfolioItems = new[]
        {
            new UpdatePortfolioItemDto
            {
                PortfolioItemId = retainedItem.PortfolioItemsId,
                Title = " Updated project ",
                Description = " Portfolio description ",
                ProjectUrl = " https://example.com/project ",
                ProjectDate = new DateOnly(2026, 7, 15)
            },
            new UpdatePortfolioItemDto
            {
                Title = "New project",
                ProjectDate = new DateOnly(2026, 8, 1)
            }
        };

        var mediaService = new FakeMediaService();
        var result = await CreateHandler(context, userId, mediaService).Handle(
            new UpdateFreelancerProfileCommand(dto),
            CancellationToken.None);

        Assert.DoesNotContain(removedItem, portfolioItems.Entities);
        Assert.Equal("Updated project", retainedItem.Title);
        Assert.Equal("Portfolio description", retainedItem.Description);
        Assert.Equal("https://example.com/project", retainedItem.ProjectUrl);
        Assert.Null(retainedItem.ImageUrl);
        Assert.Equal(new DateOnly(2026, 7, 15), retainedItem.ProjectDate);
        Assert.Equal(2, portfolioItems.Entities.Count);
        Assert.Equal(2, result.PortfolioItems.Count);
        Assert.Contains(removedItem.ImageUrl, mediaService.DeletedFiles);
        Assert.Contains(result.PortfolioItems, item =>
            item.PortfolioItemId == retainedItem.PortfolioItemsId &&
            item.ProjectDate == "2026-07-15");
    }

    private static UpdateFreelancerProfileDto CreateValidDto(Guid majorId, Guid categoryId)
    {
        return new UpdateFreelancerProfileDto
        {
            Title = " Backend Developer ",
            Bio = " Experienced .NET developer. ",
            Availability = 0,
            Location = " Ho Chi Minh City ",
            MajorId = majorId,
            CategoryIds = new[] { categoryId }
        };
    }

    private static (Guid MajorId, Guid CategoryId) AddTaxonomy(InMemoryApplicationDbContext context)
    {
        var (major, category, _) = AddTaxonomyMapping(context, "Software Development", "Backend Development");
        context.AddSet<FreelancerProfileCategory>();
        return (major.MajorsId, category.CategoriesId);
    }

    private static (Major Major, Category Category, MajorCategory Mapping) AddTaxonomyMapping(
        InMemoryApplicationDbContext context,
        string majorName,
        string categoryName)
    {
        var major = new Major
        {
            MajorsId = Guid.NewGuid(),
            Name = majorName,
            Slug = majorName.ToLowerInvariant().Replace(' ', '-'),
            IsActive = true
        };
        return AddTaxonomyMapping(context, major, categoryName);
    }

    private static (Major Major, Category Category, MajorCategory Mapping) AddTaxonomyMapping(
        InMemoryApplicationDbContext context,
        Major major,
        string categoryName)
    {
        var category = new Category
        {
            CategoriesId = Guid.NewGuid(),
            Name = categoryName,
            Slug = categoryName.ToLowerInvariant().Replace(' ', '-'),
            IsActive = true
        };
        var mapping = new MajorCategory
        {
            MajorCategoriesId = Guid.NewGuid(),
            MajorId = major.MajorsId,
            CategoryId = category.CategoriesId,
            Major = major,
            Category = category
        };

        var majors = context.Set<Major>() as TestDbSet<Major>;
        if (majors is null || !majors.Entities.Contains(major)) context.Set<Major>().Add(major);
        context.Set<Category>().Add(category);
        context.Set<MajorCategory>().Add(mapping);
        return (major, category, mapping);
    }

    private static FreelancerProfileCategory CreateSelection(Guid profileId, MajorCategory mapping) => new()
    {
        FreelancerProfileCategoriesId = Guid.NewGuid(),
        FreelancerProfileId = profileId,
        MajorCategoryId = mapping.MajorCategoriesId,
        MajorCategory = mapping,
        CreatedAt = DateTime.UtcNow.AddDays(-2)
    };

    private static FreelancerSkill CreateSkillSelection(FreelancerProfile profile, Skill skill) => new()
    {
        FreelancerSkillsId = Guid.NewGuid(),
        FreelancerId = profile.FreelancerProfilesId,
        SkillsId = skill.SkillsId,
        Freelancer = profile,
        Skills = skill
    };

    private static FreelancerProfile CreateExistingProfile(
        Guid userId,
        Major major,
        params FreelancerProfileCategory[] selections)
    {
        var profile = new FreelancerProfile
        {
            FreelancerProfilesId = selections.FirstOrDefault()?.FreelancerProfileId ?? Guid.NewGuid(),
            UserId = userId,
            Title = "Old Title",
            Bio = "Old bio",
            Availability = 2,
            Location = "Hanoi",
            MajorId = major.MajorsId,
            Major = major,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            FreelancerProfileCategories = selections.ToList()
        };
        foreach (var selection in selections)
        {
            selection.FreelancerProfile = profile;
        }
        return profile;
    }

    private static User CreateFreelancerUser(Guid userId, FreelancerProfile profile) => new()
    {
        UserId = userId,
        FullName = "Freelancer User",
        Email = "freelancer@example.com",
        Role = (int)UserRole.Freelancer,
        FreelancerProfile = profile
    };

    private static UpdateFreelancerProfileCommandHandler CreateHandler(
        InMemoryApplicationDbContext context,
        Guid userId,
        FakeMediaService? mediaService = null) => new(
            context,
            new FixedCurrentUserService(userId),
            CreateMapper(),
            NullLogger<UpdateFreelancerProfileCommandHandler>.Instance,
            mediaService ?? new FakeMediaService());

    private static IMapper CreateMapper()
    {
        return new MapperConfiguration(
            config => config.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
    }

    private sealed class FixedCurrentUserService : ICurrentUserService
    {
        public FixedCurrentUserService(Guid userId)
        {
            UserId = userId.ToString();
        }

        public string? UserId { get; }
        public string? Email => null;
        public string? Role => null;
    }
}
