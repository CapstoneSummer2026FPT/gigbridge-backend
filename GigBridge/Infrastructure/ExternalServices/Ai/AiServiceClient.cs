using System.Net.Http.Json;
using System.Net.Http.Headers;
using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Common.Models;
using Application.Common.Models.Ai;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Ai;

public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly AiServiceOptions _options;

    public AiServiceClient(HttpClient httpClient, IOptions<AiServiceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new ArgumentException("AiService BaseUrl configuration is missing.");
        }

        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
    }

    public async Task<JobPostGenerationResponseDto> GenerateJobDescriptionAsync(
        JobPostGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ai/job-posts/generate", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(
                    cancellationToken: cancellationToken);
                if (errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.Message))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        throw new BadRequestException(errorResponse.Message);
                    }
                    throw new HttpRequestException(errorResponse.Message);
                }
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch
            {
                // Fallback to default EnsureSuccessStatusCode behavior if parsing fails
            }

            response.EnsureSuccessStatusCode();
        }

        // take response from Ai server then prase it to json 
        // if not success throw HttpRequestException
        
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<JobPostGenerationResponseDto>>(
            cancellationToken: cancellationToken);

        if (apiResponse == null || !apiResponse.Success || apiResponse.Data == null)
        {
            var errorMessage = apiResponse?.Message ?? "Failed to generate job description from AI service.";
            if (apiResponse?.Errors != null)
            {
                errorMessage += " Errors: " + apiResponse.Errors.ToString();
            }
            throw new HttpRequestException(errorMessage);
        }

        return apiResponse.Data;
    }

    public async Task<AiInterviewQuestionResponseDto> StartInterviewAsync(
        AiInterviewStartRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/ai/interviews/start",
            request,
            cancellationToken);

        return await ReadInterviewResponseAsync<AiInterviewQuestionResponseDto>(
            response,
            cancellationToken);
    }

    public async Task<AiInterviewDraftResponseDto> TranscribeInterviewAudioAsync(
        string sessionId,
        Stream audioStream,
        string fileName,
        string contentType,
        string language,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(sessionId), "session_id");
        form.Add(new StringContent(language), "language");

        var audioContent = new StreamContent(audioStream);
        if (MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            audioContent.Headers.ContentType = mediaType;
        }
        else
        {
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }
        form.Add(audioContent, "audio_file", fileName);

        using var response = await _httpClient.PostAsync(
            "api/ai/interviews/transcribe-audio",
            form,
            cancellationToken);

        return await ReadInterviewResponseAsync<AiInterviewDraftResponseDto>(
            response,
            cancellationToken);
    }

    public async Task<AiInterviewQuestionResponseDto> ConfirmInterviewAnswerAsync(
        AiInterviewConfirmRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/ai/interviews/confirm-answer",
            request,
            cancellationToken);

        return await ReadInterviewResponseAsync<AiInterviewQuestionResponseDto>(
            response,
            cancellationToken);
    }

    public async Task<AiInterviewQuestionAudioResponseDto> GetInterviewQuestionAudioAsync(
        string sessionId,
        int questionIndex,
        string audioAccessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/ai/interviews/{Uri.EscapeDataString(sessionId)}/questions/{questionIndex}/audio");
        request.Headers.TryAddWithoutValidation("X-Session-Token", audioAccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadInterviewResponseAsync<AiInterviewQuestionAudioResponseDto>(
            response,
            cancellationToken);
    }

    public async Task<AiInterviewAudioStreamDto> StreamInterviewQuestionAudioAsync(
        string sessionId,
        int questionIndex,
        string audioAccessToken,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/ai/interviews/{Uri.EscapeDataString(sessionId)}/questions/{questionIndex}/audio/stream");
        request.Headers.TryAddWithoutValidation("X-Session-Token", audioAccessToken);

        var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        request.Dispose();

        if (!response.IsSuccessStatusCode)
        {
            var message = $"AI interview audio service returned {(int)response.StatusCode}.";
            try
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(
                    cancellationToken: cancellationToken);
                if (!string.IsNullOrWhiteSpace(errorResponse?.Message))
                {
                    message = errorResponse.Message;
                }
            }
            finally
            {
                response.Dispose();
            }
            throw new HttpRequestException(message, null, response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";
        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return new AiInterviewAudioStreamDto
        {
            AudioStream = new ResponseOwnedStream(responseStream, response),
            ContentType = contentType
        };
    }

    private static async Task<T> ReadInterviewResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(
                cancellationToken: cancellationToken);
            var message = !string.IsNullOrWhiteSpace(errorResponse?.Message)
                ? errorResponse.Message
                : $"AI interview service returned {(int)response.StatusCode}.";

            throw new HttpRequestException(message, null, response.StatusCode);
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(
            cancellationToken: cancellationToken);

        if (apiResponse is null || !apiResponse.Success || apiResponse.Data is null)
        {
            throw new HttpRequestException(
                apiResponse?.Message ?? "The AI interview service returned an invalid response.");
        }

        return apiResponse.Data;
    }

    private sealed class ResponseOwnedStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;

        public ResponseOwnedStream(Stream inner, HttpResponseMessage response)
        {
            _inner = inner;
            _response = response;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            _response.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
