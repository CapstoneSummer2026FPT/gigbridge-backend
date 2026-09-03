namespace Application.Features.Wallets.Common.DTOs;

/// <param name="Status">Withdrawal status after the operation, when it succeeded.</param>
public sealed record BulkWithdrawalItemResult(
    Guid WithdrawalId,
    bool Success,
    int? Status,
    string? Error);

/// <summary>
/// Outcome of an admin operation applied across many withdrawals. One failing row never aborts the
/// batch: each row reports its own result so a partial recovery is visible and repeatable.
/// </summary>
public sealed record BulkWithdrawalOperationResponse(
    int Requested,
    int Succeeded,
    int Failed,
    IReadOnlyList<BulkWithdrawalItemResult> Items);

public sealed record BulkRetryWithdrawalsRequest(IReadOnlyList<Guid> WithdrawalIds);
