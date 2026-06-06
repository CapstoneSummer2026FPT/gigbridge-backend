using Application.Features.FAQCategories.Shared.DTOs;
using MediatR;

namespace Application.Features.FAQCategories.GetById.Queries;

public sealed record GetFAQCategoryByIdQuery(int Id) : IRequest<FAQCategoryDto?>;
