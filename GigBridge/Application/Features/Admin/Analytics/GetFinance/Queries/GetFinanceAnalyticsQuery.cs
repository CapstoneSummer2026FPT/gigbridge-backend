using Application.Common.InternalServices.Admin.Analytics.Models;
using MediatR;

namespace Application.Features.Admin.Analytics.GetFinance.Queries;

public sealed record GetFinanceAnalyticsQuery(AdminAnalyticsRangeRequest Range)
    : IRequest<FinanceAnalyticsResponse>;
