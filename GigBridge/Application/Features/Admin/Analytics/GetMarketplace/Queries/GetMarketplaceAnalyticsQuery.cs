using Application.Common.InternalServices.Admin.Analytics.Models;
using MediatR;

namespace Application.Features.Admin.Analytics.GetMarketplace.Queries;

public sealed record GetMarketplaceAnalyticsQuery(AdminAnalyticsRangeRequest Range)
    : IRequest<MarketplaceAnalyticsResponse>;
