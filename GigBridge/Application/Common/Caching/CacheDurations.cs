namespace Application.Common.Caching;

public static class CacheDurations
{
    public static TimeSpan ShortList => TimeSpan.FromMinutes(3);
    public static TimeSpan DefaultDetail => TimeSpan.FromMinutes(10);
    public static TimeSpan Version => TimeSpan.FromHours(2);
}
