using System;
using System.Collections.Generic;
using Application.Features.Contracts.Freelancer.GetMyCompletedProjects.DTOs;
using MediatR;

namespace Application.Features.Contracts.Freelancer.GetMyCompletedProjects.Queries;

public record GetMyCompletedProjectsQuery(Guid UserId)
    : IRequest<List<FreelancerCompletedProjectResponse>>;
