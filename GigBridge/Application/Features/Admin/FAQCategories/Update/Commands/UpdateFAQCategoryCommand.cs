using Application.Features.Admin.FAQCategories.Update.DTOs;
using Application.Features.FAQCategories.Shared.DTOs;
using MediatR;

namespace Application.Features.Admin.FAQCategories.Update.Commands;

public sealed record UpdateFAQCategoryCommand(int Id, UpdateFAQCategoryRequest Request) : IRequest<FAQCategoryDto?>;
