using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Test_Gigbridge_Backend.Infrastructure.Receipts;

public sealed class ProjectReceiptPersistenceModelTests
{
    [Fact]
    public void Model_RegistersProjectReceiptWithItsCriticalConstraints()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseInMemoryDatabase($"project-receipt-model-{Guid.NewGuid():N}")
            .Options;
        using var context = new GigbridgeDbContext(options);

        var entity = context.Model.FindEntityType(typeof(ProjectReceipt));

        Assert.NotNull(entity);
        Assert.Equal("ProjectReceipts", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(ProjectReceipt.GenerationLeaseToken))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ProjectReceipt.ContractsId),
                nameof(ProjectReceipt.ReceiptType)
            ]));
    }
}
