using Application.Common.Interfaces;
namespace Infrastructure.Services.Common;
public class DateTimeService : IDateTimeService {
    public DateTime UtcNow => DateTime.UtcNow;
}