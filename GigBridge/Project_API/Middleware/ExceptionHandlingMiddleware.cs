using Application.Common.Models;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Application.Common.Exceptions;

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
        catch (Exception ex)
        {
            if (ex is not ValidationException &&
                ex is not ConflictException &&
                ex is not BadRequestException &&
                ex is not UnauthorizedAccessException &&
                ex is not NotFoundException &&
                ex is not ForbiddenAccessException)
            {
                _logger.LogError(ex, "An unhandled exception occurred.");
            }
            else
            {
                _logger.LogWarning("Business exception occurred: {Message}", ex.Message);
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
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;

        object response;
        if (exception is ValidationException validationException)
        {
            response = ApiResponse<object>.Error((int)statusCode, "Validation failed", validationException.Errors);
        }
        else
        {
            var message = statusCode == HttpStatusCode.InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : exception.Message;
            response = ApiResponse<object>.Error((int)statusCode, message);
        }

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
