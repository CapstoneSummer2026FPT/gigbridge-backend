using Application.Features.Wallets.Common.FinancialOverview.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.FinancialOverview.Queries;

public sealed record GetFinancialOverviewQuery(
    Guid UserId,
    FinancialOverviewPeriod Period) : IRequest<FinancialOverviewResponse>;
