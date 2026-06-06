using Application.Features.FAQs.Shared.DTOs;
using MediatR;

namespace Application.Features.FAQs.GetAll.Queries;

public sealed record GetAllFAQsQuery(int? FaqCategoryId = null) : IRequest<IReadOnlyList<FAQDto>>;
