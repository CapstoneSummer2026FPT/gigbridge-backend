using System.Net.Http.Json;
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
        response.EnsureSuccessStatusCode();
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
}
