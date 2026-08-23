using Application.Common.Models;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Application.Common.Exceptions;
using Sentry;

namespace Project_API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Request {Method} {Path} was canceled by the client.",
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception ex)
        {
            if (ex is not ValidationException &&
                ex is not ConflictException &&
                ex is not BadRequestException &&
                ex is not UnauthorizedAccessException &&
                ex is not NotFoundException &&
                ex is not ExternalServiceException &&
                ex is not ForbiddenAccessException)
            {
                _logger.LogError(ex, "An unhandled exception occurred.");
            }
            else
            {
                _logger.LogWarning("Business exception occurred: {Message}", ex.Message);
            }

            if (ex is ExternalServiceException ||
                ex is not ValidationException and
                    not ConflictException and
                    not BadRequestException and
                    not UnauthorizedAccessException and
                    not NotFoundException and
                    not ForbiddenAccessException)
            {
                SentrySdk.CaptureException(ex);
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = exception switch
        {
            ValidationException => HttpStatusCode.BadRequest,
            BadRequestException => HttpStatusCode.BadRequest,
            ConflictException => HttpStatusCode.Conflict,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ForbiddenAccessException => HttpStatusCode.Forbidden,
            NotFoundException => HttpStatusCode.NotFound,
            ExternalServiceException => HttpStatusCode.ServiceUnavailable,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;

        object response;
        if (exception is ValidationException validationException)
        {
            var message = validationException.Errors
                .SelectMany(error => error.Value)
                .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error))
                ?? "Validation failed";

            response = ApiResponse<object>.Error((int)statusCode, message, validationException.Errors);
        }
        else
        {
            var message = statusCode == HttpStatusCode.InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : exception.Message;
            response = ApiResponse<object>.Error((int)statusCode, message);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
