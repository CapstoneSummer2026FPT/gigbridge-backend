using Application.Features.Admin.Analytics.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Analytics.GetFinance.Queries;

public sealed record GetFinanceAnalyticsQuery(AdminAnalyticsRangeRequest Range)
    : IRequest<FinanceAnalyticsResponse>;
