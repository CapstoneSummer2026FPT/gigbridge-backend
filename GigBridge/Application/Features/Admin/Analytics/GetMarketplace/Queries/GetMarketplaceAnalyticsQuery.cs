using Application.Features.Admin.Analytics.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Analytics.GetMarketplace.Queries;

public sealed record GetMarketplaceAnalyticsQuery(AdminAnalyticsRangeRequest Range)
    : IRequest<MarketplaceAnalyticsResponse>;
