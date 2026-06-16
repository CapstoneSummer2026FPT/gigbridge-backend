using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class UserWallet
{
    public Guid UserWalletsId { get; set; }

    public Guid UserId { get; set; }

    public decimal AvailableTokens { get; set; }

    public decimal HeldTokens { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
