using Application.Common.Interfaces.Time;
namespace Infrastructure.Adapters.Time;
public class DateTimeService : IDateTimeService {
    public DateTime UtcNow => DateTime.UtcNow;
}
