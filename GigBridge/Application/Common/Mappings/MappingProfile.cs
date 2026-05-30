using AutoMapper;

using Domain.Entities;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.Profile.UpdateClientProfile.DTOs;
using Application.Features.Profile.UpdateFreelancerProfile.DTOs;



namespace Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {

        CreateMap<User, UserDTO>();
        CreateMap<User, AdminUserDto>();
        
        // Profile mappings
        CreateMap<ClientProfile, ClientProfileResponseDto>();
        CreateMap<FreelancerProfile, FreelancerProfileResponseDto>();

    }
}