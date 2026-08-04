using Application.Features.Admin.Reconciliation.Common.DTOs;
using MediatR;

namespace Application.Features.Admin.Reconciliation.Queries;

/// <summary>
/// Builds the read-only financial reconciliation report. This query never writes to the
/// wallet, ledger, escrow, or contract tables — it only reads and reports existing drift.
/// </summary>
public sealed record GetEscrowReconciliationReportQuery : IRequest<EscrowReconciliationReport>;
