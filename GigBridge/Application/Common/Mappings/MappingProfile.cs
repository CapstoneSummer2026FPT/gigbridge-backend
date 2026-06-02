using AutoMapper;

using Domain.Entities;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.FAQCategories.Shared.DTOs;
using Application.Features.FAQs.Shared.DTOs;



namespace Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {

        CreateMap<User, UserDTO>();
        CreateMap<User, AdminUserDto>();

        // FAQCategory mappings
        CreateMap<Faqcategory, FAQCategoryDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.FaqcategoriesId));

        // FAQ mappings
        CreateMap<Faq, FAQDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.FaqsId))
            .ForMember(dest => dest.FaqCategoryId, opt => opt.MapFrom(src => src.FaqcategoriesId))
            .ForMember(dest => dest.FaqCategoryName, opt => opt.MapFrom(src => src.Faqcategories != null ? src.Faqcategories.Name : null));

    }
}