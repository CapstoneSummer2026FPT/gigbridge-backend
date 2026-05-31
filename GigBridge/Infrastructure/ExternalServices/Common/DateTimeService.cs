using Application.Common.Interfaces.IService;
namespace Infrastructure.Services.Common;
public class DateTimeService : IDateTimeService {
    public DateTime UtcNow => DateTime.UtcNow;
}