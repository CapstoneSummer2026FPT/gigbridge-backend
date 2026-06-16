using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Wallets.Common;
using Application.Features.Wallets.Common.DTOs;
using MediatR;

namespace Application.Features.Wallets.Common.GetMine.Queries;

public sealed class GetMyWalletQueryHandler : IRequestHandler<GetMyWalletQuery, WalletResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public GetMyWalletQueryHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task<WalletResponse> Handle(GetMyWalletQuery request, CancellationToken cancellationToken)
    {
        var wallet = await WalletWorkflow.GetOrCreateWalletAsync(
            _context,
            request.UserId,
            _dateTimeService.UtcNow,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return WalletResponse.FromEntity(wallet);
    }
}
