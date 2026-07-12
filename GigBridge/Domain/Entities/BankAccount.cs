using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class BankAccount
{
    public Guid BankAccountId { get; set; }

    public Guid UserId { get; set; }

    public string BankCode { get; set; } = null!;

    public string BankName { get; set; } = null!;

    public string AccountNumberEncrypted { get; set; } = null!;

    public string AccountNumberMasked { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    /// <summary>
    /// Enum BankAccountStatus: 0=Active, 1=Disabled
    /// </summary>
    public int Status { get; set; }

    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WalletWithdrawal> WalletWithdrawals { get; set; } = new List<WalletWithdrawal>();
}
