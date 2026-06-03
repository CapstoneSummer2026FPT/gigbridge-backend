using AutoMapper;

using Domain.Entities;
using Application.Features.Auth.Shared.DTOs;
using Application.Features.Admin.Users.Shared.DTOs;
using Application.Features.Profiles.FreelancerProfile.DTOs;
using Application.Features.Profiles.ClientProfile.DTOs;

namespace Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDTO>();
        CreateMap<User, AdminUserDto>();
        CreateMap<FreelancerProfile, FreelancerProfileResponseDto>();
        CreateMap<ClientProfile, ClientProfileResponseDto>();
    }
}