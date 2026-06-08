using Application.Common.Models;

namespace Test_Gigbridge_Backend.Application.Common.Models;

public class PaginatedListTests
{
    [Fact]
    public void Constructor_CalculatesTotalPagesByRoundingUp()
    {
        var items = new List<int> { 1, 2, 3 };

        var page = new PaginatedList<int>(items, count: 11, pageNumber: 2, pageSize: 5);

        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public void HasPreviousPage_IsTrueWhenPageNumberGreaterThanOne()
    {
        var page = new PaginatedList<int>(new List<int>(), count: 20, pageNumber: 2, pageSize: 10);

        Assert.True(page.HasPreviousPage);
    }

    [Fact]
    public void HasNextPage_IsFalseOnLastPage()
    {
        var page = new PaginatedList<int>(new List<int>(), count: 20, pageNumber: 2, pageSize: 10);

        Assert.False(page.HasNextPage);
    }
}
