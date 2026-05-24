using AutoMapper;
using Domain.Entities;
using Application.Features.Auth.DTOs.AuthDTOs;

namespace Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDTO>();
    }
}
