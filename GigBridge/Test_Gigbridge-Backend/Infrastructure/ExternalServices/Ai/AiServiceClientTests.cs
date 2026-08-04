using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Common.Models.Ai;
using Infrastructure.ExternalServices.Ai;
using Microsoft.Extensions.Options;
using Xunit;

namespace Test_Gigbridge_Backend.Infrastructure.ExternalServices.Ai;

public class AiServiceClientTests
{
    private readonly IOptions<AiServiceOptions> _options;

    public AiServiceClientTests()
    {
        _options = Options.Create(new AiServiceOptions
        {
            BaseUrl = "http://localhost:8000",
            ApiKey = "test-key"
        });
    }

    [Fact]
    public async Task CreateInterviewDefinitionAsync_RegistersPremiumConfiguration()
    {
        var expectedResponse = new ApiResponse<AiInterviewDefinitionResponseDto>
        {
            Success = true,
            StatusCode = 201,
            Data = new AiInterviewDefinitionResponseDto
            {
                DefinitionReference = "aidef_123_signature",
                Status = "active",
                Language = "en",
                Mode = "voice",
                QuestionCount = 5
            }
        };
        var handler = new MockHttpMessageHandler(HttpStatusCode.Created, expectedResponse);
        var client = new AiServiceClient(new HttpClient(handler), _options);

        var result = await client.CreateInterviewDefinitionAsync(
            new AiInterviewDefinitionRequestDto
            {
                JobId = "job-123",
                JobTitle = "Senior React Engineer",
                JobSkills = ["React", "TypeScript"],
                Mode = "voice",
                Language = "en",
                QuestionCount = 5
            },
            CancellationToken.None);

        Assert.Equal("aidef_123_signature", result.DefinitionReference);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal(
            "http://localhost:8000/api/ai/interviews/definitions",
            handler.RequestUri?.ToString());
        Assert.Contains("\"question_count\":5", handler.RequestBody);
    }

    [Fact]
    public async Task StartInterviewAsync_PostsToInterviewEndpoint_AndReturnsFirstQuestion()
    {
        var expectedResponse = new ApiResponse<AiInterviewQuestionResponseDto>
        {
            Success = true,
            StatusCode = 201,
            Message = "Interview session successfully initialized.",
            Data = new AiInterviewQuestionResponseDto
            {
                SessionId = "session-123",
                AudioAccessToken = "audio-token-123",
                QuestionIndex = 1,
                QuestionText = "Tell me about a difficult React performance issue you solved.",
                Language = "en"
            }
        };

        var handler = new MockHttpMessageHandler(HttpStatusCode.Created, expectedResponse);
        var client = new AiServiceClient(new HttpClient(handler), _options);
        var request = new AiInterviewStartRequestDto
        {
            JobId = "job-123",
            FreelancerId = "freelancer-123",
            JobTitle = "Senior React Engineer",
            JobSkills = new List<string> { "React", "TypeScript" },
            Mode = "voice",
            Language = "en",
            QuestionCount = 5,
            DefinitionReference = "aidef_123_signature"
        };

        var result = await client.StartInterviewAsync(request, CancellationToken.None);

        Assert.Equal("session-123", result.SessionId);
        Assert.Equal(1, result.QuestionIndex);
        Assert.Equal("Tell me about a difficult React performance issue you solved.", result.QuestionText);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("http://localhost:8000/api/ai/interviews/start", handler.RequestUri?.ToString());
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Contains("\"question_count\":5", handler.RequestBody);
        Assert.Contains("\"definition_reference\":\"aidef_123_signature\"", handler.RequestBody);
    }

    [Fact]
    public async Task TranscribeInterviewAudioAsync_ForwardsMultipartAudio()
    {
        var expectedResponse = new ApiResponse<AiInterviewDraftResponseDto>
        {
            Success = true,
            StatusCode = 200,
            Data = new AiInterviewDraftResponseDto
            {
                SessionId = "session-123",
                DraftId = "draft-123",
                QuestionIndex = 1,
                Transcript = "A real transcribed answer",
                Language = "en",
                SttProvider = "faster_whisper",
                Confidence = 0.91
            }
        };
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var client = new AiServiceClient(new HttpClient(handler), _options);
        await using var audio = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var result = await client.TranscribeInterviewAudioAsync(
            "session-123",
            audio,
            "answer.webm",
            "audio/webm",
            "en",
            CancellationToken.None);

        Assert.Equal("A real transcribed answer", result.Transcript);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("http://localhost:8000/api/ai/interviews/transcribe-audio", handler.RequestUri?.ToString());
        Assert.StartsWith("multipart/form-data", handler.RequestContentType);
        Assert.Contains("session_id", handler.RequestBody);
        Assert.Contains("audio_file", handler.RequestBody);
        Assert.Contains("answer.webm", handler.RequestBody);
    }

    [Fact]
    public async Task TranscribeInterviewAudioAsync_PreservesAudioValidationMessage()
    {
        var errorResponse = ApiResponse<object>.BadRequest("Audio Decode Failed");
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, errorResponse);
        var client = new AiServiceClient(new HttpClient(handler), _options);
        await using var audio = new MemoryStream(new byte[] { 1, 2, 3, 4 });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            client.TranscribeInterviewAudioAsync(
                "session-123",
                audio,
                "answer.webm",
                "audio/webm",
                "en",
                CancellationToken.None));

        Assert.Equal("Audio Decode Failed", exception.Message);
    }

    [Fact]
    public async Task ConfirmInterviewAnswerAsync_ReturnsNextQuestion()
    {
        var expectedResponse = new ApiResponse<AiInterviewQuestionResponseDto>
        {
            Success = true,
            StatusCode = 200,
            Data = new AiInterviewQuestionResponseDto
            {
                SessionId = "session-123",
                QuestionIndex = 2,
                QuestionText = "What trade-off did you make?"
            }
        };
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var client = new AiServiceClient(new HttpClient(handler), _options);

        var result = await client.ConfirmInterviewAnswerAsync(
            new AiInterviewConfirmRequestDto
            {
                SessionId = "session-123",
                CorrectedText = "My reviewed answer"
            },
            CancellationToken.None);

        Assert.Equal(2, result.QuestionIndex);
        Assert.Equal("http://localhost:8000/api/ai/interviews/confirm-answer", handler.RequestUri?.ToString());
        Assert.Contains("My reviewed answer", handler.RequestBody);
    }

    [Fact]
    public async Task GetInterviewQuestionAudioAsync_ForwardsSessionToken()
    {
        var expectedResponse = new ApiResponse<AiInterviewQuestionAudioResponseDto>
        {
            Success = true,
            StatusCode = 200,
            Data = new AiInterviewQuestionAudioResponseDto
            {
                SessionId = "session-123",
                QuestionIndex = 2,
                Status = "ready",
                AudioBase64 = "YXVkaW8=",
                AudioMimeType = "audio/mpeg"
            }
        };
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var client = new AiServiceClient(new HttpClient(handler), _options);

        var result = await client.GetInterviewQuestionAudioAsync(
            "session-123",
            2,
            "session-audio-token",
            CancellationToken.None);

        Assert.Equal("ready", result.Status);
        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal(
            "http://localhost:8000/api/ai/interviews/session-123/questions/2/audio",
            handler.RequestUri?.ToString());
        Assert.Equal("session-audio-token", handler.SessionToken);
    }

    [Fact]
    public async Task StreamInterviewQuestionAudioAsync_ReturnsUpstreamStream()
    {
        var handler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            new byte[] { 1, 2, 3, 4 },
            "audio/mpeg");
        var client = new AiServiceClient(new HttpClient(handler), _options);

        var result = await client.StreamInterviewQuestionAudioAsync(
            "session-123",
            2,
            "session-audio-token",
            CancellationToken.None);
        await using var stream = result.AudioStream;
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);

        Assert.Equal("audio/mpeg", result.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, output.ToArray());
        Assert.Equal(
            "http://localhost:8000/api/ai/interviews/session-123/questions/2/audio/stream",
            handler.RequestUri?.ToString());
        Assert.Equal("session-audio-token", handler.SessionToken);
    }

    [Fact]
    public async Task AnalyzeVettingAsync_PostsToVettingEvaluationEndpoint_AndReturnsEvaluation()
    {
        var expectedResponse = new ApiResponse<VettingEvaluationResponseDto>
        {
            Success = true,
            StatusCode = 200,
            Message = "Success",
            Data = new VettingEvaluationResponseDto
            {
                Score = 85,
                Summary = "Summary details",
                TechnicalSkills = new List<string> { "React" },
                SoftSkills = new List<string> { "Communication" },
                RecommendedHire = true,
                HolisticAdjustment = 5,
                HolisticAdjustmentReason = "Reason",
                GradedQuestions = new List<GradedQuestionDto>
                {
                    new GradedQuestionDto
                    {
                        QuestionIndex = 1,
                        QuestionText = "Q1",
                        QuestionType = "theoretical",
                        Difficulty = "easy",
                        CandidateAnswer = "A1",
                        Score = 80,
                        Feedback = "Feedback"
                    }
                }
            }
        };

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var client = new AiServiceClient(new HttpClient(handler), _options);

        var request = new AnalyzeVettingRequestDto
        {
            FreelancerId = "freelancer-123",
            JobTitle = "React Developer",
            JobDescription = "React job",
            JobSkills = new List<string> { "React" },
            QaPairs = new List<QuestionAnswerPairDto>
            {
                new QuestionAnswerPairDto
                {
                    QuestionIndex = 1,
                    QuestionText = "Q1",
                    CandidateAnswer = "A1"
                }
            }
        };

        var result = await client.AnalyzeVettingAsync(request, CancellationToken.None);

        Assert.Equal(85, result.Score);
        Assert.Equal("Summary details", result.Summary);
        Assert.Single(result.TechnicalSkills);
        Assert.Contains("React", result.TechnicalSkills);
        Assert.True(result.RecommendedHire);
        Assert.Equal(5, result.HolisticAdjustment);
        Assert.Equal("Reason", result.HolisticAdjustmentReason);
        Assert.Single(result.GradedQuestions);
        Assert.Equal("Q1", result.GradedQuestions[0].QuestionText);
        Assert.Equal(80, result.GradedQuestions[0].Score);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("http://localhost:8000/api/ai/interviews/ai-interview-judging", handler.RequestUri?.ToString());
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Contains("freelancer_id", handler.RequestBody);
        Assert.Contains("freelancer-123", handler.RequestBody);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object _responseContent;
        private readonly string? _responseContentType;

        public HttpMethod? RequestMethod { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? SessionToken { get; private set; }
        public string? RequestContentType { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        public MockHttpMessageHandler(
            HttpStatusCode statusCode,
            object responseContent,
            string? responseContentType = null)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
            _responseContentType = responseContentType;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("X-API-Key", out var values)
                ? values.SingleOrDefault()
                : null;
            SessionToken = request.Headers.TryGetValues("X-Session-Token", out var sessionTokens)
                ? sessionTokens.SingleOrDefault()
                : null;
            RequestContentType = request.Content?.Headers.ContentType?.ToString();
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            HttpContent content = _responseContent is byte[] bytes
                ? new ByteArrayContent(bytes)
                : JsonContent.Create(_responseContent);
            if (!string.IsNullOrWhiteSpace(_responseContentType))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(_responseContentType);
            }
            var response = new HttpResponseMessage(_statusCode) { Content = content };
            return await Task.FromResult(response);
        }
    }
}
