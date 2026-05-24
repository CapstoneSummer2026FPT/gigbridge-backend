using AutoMapper;
using Application.DTOs.Admin;
using Domain.Entities;

namespace Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<JobPost, JobPostDto>()
            .ForMember(destination => destination.JobPostId, options => options.MapFrom(source => source.JobPostsId))
            .ForMember(destination => destination.ClientProfileId, options => options.MapFrom(source => source.ClientProfilesId))
            .ForMember(destination => destination.ClientCompanyName, options => options.MapFrom(source => source.ClientProfiles.CompanyName))
            .ForMember(destination => destination.CategoryName, options => options.MapFrom(source => source.Category == null ? null : source.Category.Name));
        CreateMap<JobPost, JobPostDetailDto>()
            .ForMember(destination => destination.JobPostId, options => options.MapFrom(source => source.JobPostsId))
            .ForMember(destination => destination.ClientProfileId, options => options.MapFrom(source => source.ClientProfilesId))
            .ForMember(destination => destination.ClientCompanyName, options => options.MapFrom(source => source.ClientProfiles.CompanyName))
            .ForMember(destination => destination.CategoryName, options => options.MapFrom(source => source.Category == null ? null : source.Category.Name))
            .ForMember(destination => destination.ProposalCount, options => options.MapFrom(source => source.Proposals.Count));

        CreateMap<Review, ReviewDto>()
            .ForMember(destination => destination.ReviewId, options => options.MapFrom(source => source.ReviewsId))
            .ForMember(destination => destination.ContractId, options => options.MapFrom(source => source.ContractsId))
            .ForMember(destination => destination.ContractTitle, options => options.MapFrom(source => source.Contracts.Title));

        CreateMap<Report, ReportDto>()
            .ForMember(destination => destination.ReportId, options => options.MapFrom(source => source.ReportsId));
    }
}
