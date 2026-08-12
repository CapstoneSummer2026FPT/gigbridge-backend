using Application.Common.Interfaces.Time;
namespace Infrastructure.Services.Common;
public class DateTimeService : IDateTimeService {
    public DateTime UtcNow => DateTime.UtcNow;
}