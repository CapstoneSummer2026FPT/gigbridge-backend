using System.Diagnostics;
using System.Diagnostics.Metrics;
using Domain.Enums.ESign;

namespace Application.Common.InternalServices.ESign.Services;

public static class ESignTelemetry
{
    public const string MeterName = "GigBridge.ESign";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> EndpointRequests = Meter.CreateCounter<long>("esign.endpoint.requests");
    private static readonly Histogram<long> ResponseBytes = Meter.CreateHistogram<long>("esign.endpoint.response.bytes", "By");
    private static readonly Counter<long> ArtifactReads = Meter.CreateCounter<long>("esign.artifact.reads");
    private static readonly Counter<long> ArtifactReadBytes = Meter.CreateCounter<long>("esign.artifact.read.bytes", "By");
    private static readonly Counter<long> RevisionEvents = Meter.CreateCounter<long>("esign.revision.events");

    public static void RecordEndpoint(string endpoint, long responseBytes)
    {
        var tags = new TagList { { "endpoint", endpoint } };
        EndpointRequests.Add(1, tags);
        ResponseBytes.Record(responseBytes, tags);
    }

    public static void RecordArtifactRead(ESignArtifactType artifactType, string endpoint, long bytes)
    {
        var tags = new TagList
        {
            { "artifact.type", artifactType.ToString() },
            { "endpoint", endpoint }
        };
        ArtifactReads.Add(1, tags);
        ArtifactReadBytes.Add(bytes, tags);
    }

    public static void RecordRevisionEvent(string outcome, long count = 1) =>
        RevisionEvents.Add(count, new TagList { { "outcome", outcome } });
}
