using AutoMapper;

using Domain.Entities;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.FAQCategories.Shared.DTOs;
using Application.Features.FAQs.Shared.DTOs;
using Application.Features.Profiles.ClientProfile.Common.DTOs;
using Application.Features.Profiles.FreelancerProfile.Common.DTOs;
using Application.Features.Profiles.FreelancerProfile.GetFreelancerProfile.DTOs;
using Domain.Services;

namespace Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDTO>()
            .ForMember(
                dest => dest.EloPoints,
                opt => opt.MapFrom(src => src.UserEloScore != null
                    ? src.UserEloScore.CurrentPoints
                    : UserEloCalculator.DefaultPoints));
        CreateMap<User, AdminUserDto>();

        // FAQCategory mappings
        CreateMap<Faqcategory, FAQCategoryDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.FaqcategoriesId));

        // FAQ mappings
        CreateMap<Faq, FAQDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.FaqsId))
            .ForMember(dest => dest.FaqCategoryId, opt => opt.MapFrom(src => src.FaqcategoriesId))
            .ForMember(dest => dest.FaqCategoryName, opt => opt.MapFrom(src => src.Faqcategories != null ? src.Faqcategories.Name : null));

        CreateMap<FreelancerProfileCategory, FreelancerProfileCategoryDto>()
            .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.MajorCategory.CategoryId))
              .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.MajorCategory.Category.Name));
        CreateMap<FreelancerSkill, FreelancerSkillDto>()
            .ForMember(dest => dest.SkillId, opt => opt.MapFrom(src => src.SkillsId))
            .ForMember(dest => dest.SkillName, opt => opt.MapFrom(src => src.Skills.Name));
        CreateMap<FreelancerProfile, FreelancerProfileResponseDto>()
            .ForMember(dest => dest.MajorName, opt => opt.MapFrom(src => src.Major != null ? src.Major.Name : null))
            .ForMember(dest => dest.Categories, opt => opt.MapFrom(src => src.FreelancerProfileCategories))
            .ForMember(dest => dest.Skills, opt => opt.MapFrom(src => src.FreelancerSkills));
        CreateMap<ClientProfile, ClientProfileResponseDto>();
    }
}
