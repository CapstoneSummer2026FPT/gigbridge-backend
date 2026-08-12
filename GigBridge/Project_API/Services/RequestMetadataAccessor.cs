using Application.Features.Admin.AuditLogs.Common.Interfaces;

namespace Project_API.Services;

public sealed class RequestMetadataAccessor : IRequestMetadataAccessor
{
    private const string CorrelationItemKey = "AdminAuditCorrelationId";
    private const int MaxUserAgentLength = 512;
    private readonly IHttpContextAccessor _http;
    private readonly Guid _backgroundCorrelationId = Guid.NewGuid();
    public RequestMetadataAccessor(IHttpContextAccessor http) => _http = http;
    public Guid CorrelationId
    {
        get
        {
            var context = _http.HttpContext;
            if (context is null) return _backgroundCorrelationId;
            if (context.Items.TryGetValue(CorrelationItemKey, out var stored) && stored is Guid value) return value;
            var requested = context.Request.Headers["X-Correlation-ID"].ToString();
            var correlationId = Guid.TryParse(requested, out var parsed) ? parsed : Guid.NewGuid();
            context.Items[CorrelationItemKey] = correlationId;
            return correlationId;
        }
    }

    public string? UserAgent
    {
        get
        {
            var value = _http.HttpContext?.Request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(value)) return null;
            var sanitized = new string(value.Where(character => !char.IsControl(character)).ToArray()).Trim();
            return sanitized.Length == 0 ? null : sanitized[..Math.Min(sanitized.Length, MaxUserAgentLength)];
        }
    }
}
