using Application.Common.Interfaces;
using Application.Features.Elo.Common;
using Application.Features.Elo.DTOs;
using MediatR;

namespace Application.Features.Admin.Elo.Queries.GetEloPolicy;

public sealed class GetEloPolicyQueryHandler : IRequestHandler<GetEloPolicyQuery, EloPolicyDto>
{
    private readonly IApplicationDbContext _context;

    public GetEloPolicyQueryHandler(IApplicationDbContext context) => _context = context;

    public Task<EloPolicyDto> Handle(GetEloPolicyQuery request, CancellationToken cancellationToken)
        => EloPolicy.LoadAsync(_context, cancellationToken);
}
