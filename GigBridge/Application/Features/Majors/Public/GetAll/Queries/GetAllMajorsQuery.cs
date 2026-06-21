using Application.Features.Majors.Common.DTOs;
using MediatR;

namespace Application.Features.Majors.Public.GetAll.Queries;

public sealed record GetAllMajorsQuery : IRequest<IReadOnlyList<MajorDto>>;
