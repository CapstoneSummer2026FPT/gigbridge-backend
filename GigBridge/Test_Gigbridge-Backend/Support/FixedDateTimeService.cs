using Application.Common.Interfaces.IService;

namespace Test_Gigbridge_Backend.Support;

public sealed class FixedDateTimeService : IDateTimeService
{
    public FixedDateTimeService(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}
