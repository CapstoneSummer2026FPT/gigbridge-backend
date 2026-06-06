using Application.Features.FAQs.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.FAQs.GetAll.Queries;

public sealed record GetAllAdminFAQsQuery(int? FaqCategoryId = null) : IRequest<IReadOnlyList<FAQDto>>;
