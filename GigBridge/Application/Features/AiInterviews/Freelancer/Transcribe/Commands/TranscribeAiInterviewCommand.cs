using Application.Common.Models.Ai;
using MediatR;

namespace Application.Features.AiInterviews.Freelancer.Transcribe.Commands;

public sealed record TranscribeAiInterviewCommand(
    Guid UserId,
    string SessionId,
    Stream AudioStream,
    string FileName,
    string ContentType,
    string Language) : IRequest<AiInterviewDraftResponseDto>;
