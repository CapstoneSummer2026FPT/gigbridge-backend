using Application.Features.Admin.Analytics.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Analytics.GetPremium.Queries;

public sealed record GetPremiumAnalyticsQuery(AdminAnalyticsRangeRequest Range)
    : IRequest<PremiumAnalyticsResponse>;
