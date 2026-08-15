using Application.Common.InternalServices.Admin.Analytics.Models;
using MediatR;

namespace Application.Features.Admin.Analytics.GetPremium.Queries;

public sealed record GetPremiumAnalyticsQuery(AdminAnalyticsRangeRequest Range)
    : IRequest<PremiumAnalyticsResponse>;
