using AutoMapper;

using Domain.Entities;
using Application.Features.Auth.DTOs;
using Application.DTOs.Admin;



namespace Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {

        CreateMap<User, UserDTO>();

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

        CreateMap<Dispute, DisputeDto>()
            .ForMember(destination => destination.DisputeId, options => options.MapFrom(source => source.DisputesId))
            .ForMember(destination => destination.ContractId, options => options.MapFrom(source => source.ContractsId))
            .ForMember(destination => destination.MilestoneId, options => options.MapFrom(source => source.MilestonesId));
        CreateMap<Dispute, DisputeDetailDto>()
            .IncludeBase<Dispute, DisputeDto>()
            .ForMember(destination => destination.Contract, options => options.MapFrom(source => source.Contracts))
            .ForMember(destination => destination.Milestone, options => options.MapFrom(source => source.Milestones))
            .ForMember(destination => destination.Evidence, options => options.MapFrom(source => source.DisputeEvidences))
            .ForMember(destination => destination.Messages, options => options.MapFrom(source => source.DisputeMessages))
            .ForMember(destination => destination.PaymentProofs,
                options => options.MapFrom(source => source.Milestones == null ? null : source.Milestones.PaymentProofs));
        CreateMap<Contract, DisputeContractDto>()
            .ForMember(destination => destination.ContractId, options => options.MapFrom(source => source.ContractsId));
        CreateMap<Milestone, DisputeMilestoneDto>()
            .ForMember(destination => destination.MilestoneId, options => options.MapFrom(source => source.MilestonesId));
        CreateMap<DisputeEvidence, DisputeEvidenceDto>();
        CreateMap<DisputeMessage, DisputeMessageDto>()
            .ForMember(destination => destination.DisputeMessageId, options => options.MapFrom(source => source.DisputeMessagesId));
        CreateMap<PaymentProof, PaymentProofDto>()
            .ForMember(destination => destination.PaymentProofId, options => options.MapFrom(source => source.PaymentProofsId));

        CreateMap<AdminAuditLog, AuditLogDto>()
            .ForMember(destination => destination.AuditLogId, options => options.MapFrom(source => source.AdminAuditLogsId));

        CreateMap<Faq, FaqDto>()
            .ForMember(destination => destination.FaqId, options => options.MapFrom(source => source.FaqsId))
            .ForMember(destination => destination.CategoryId, options => options.MapFrom(source => source.FaqcategoriesId))
            .ForMember(destination => destination.CategoryName, options => options.MapFrom(source => source.Faqcategories.Name));

    }
}
