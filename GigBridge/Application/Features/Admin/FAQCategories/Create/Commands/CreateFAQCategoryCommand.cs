using Application.Features.Admin.FAQCategories.Create.DTOs;
using Application.Features.FAQCategories.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.FAQCategories.Create.Commands;

public sealed record CreateFAQCategoryCommand(CreateFAQCategoryRequest Request) : IRequest<FAQCategoryDto>;
