namespace Application.Common.Caching;

public static class CacheKeys
{
    public const string AdminUsersVersion = "admin_users:version";

    public static string AdminUsers(string version, int page, int pageSize, string? search, int? status)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? "all"
            : search.Trim().ToLowerInvariant();
        var normalizedStatus = status?.ToString() ?? "all";

        return $"admin_users:v{version}:{page}:{pageSize}:{normalizedSearch}:{normalizedStatus}";
    }

    public const string AvailableJobPostsVersion = "avail_jobs:version";
    public static string AvailableJobPosts(string version, int pageIndex, int pageSize)
        => $"avail_jobs:v{version}:{pageIndex}:{pageSize}";
    public static string JobPostDetail(Guid jobPostId)
        => $"job_detail:{jobPostId}";
}
