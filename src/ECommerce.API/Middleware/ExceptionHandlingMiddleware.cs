using System.Diagnostics;
using System.Text.Json;
using ECommerce.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred during request {Path}: {Message}", context.Request.Path, ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title, detail) = ex switch
        {
            NotFoundException nfe => (StatusCodes.Status404NotFound, "Resource Not Found", nfe.Message),
            BadRequestException bre => (StatusCodes.Status400BadRequest, "Bad Request", bre.Message),
            ValidationException ve => (StatusCodes.Status400BadRequest, "Validation Failed", "One or more validation errors occurred."),
            InsufficientStockException ise => (StatusCodes.Status409Conflict, "Stock Conflict", ise.Message),
            ConflictException ce => (StatusCodes.Status409Conflict, "Conflict", ce.Message),
            UnauthorizedException ue => (StatusCodes.Status401Unauthorized, "Unauthorized", ue.Message),
            UnauthorizedAccessException uae => (StatusCodes.Status401Unauthorized, "Unauthorized", uae.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency Conflict", "The resource was modified concurrently by another operation. Please review and retry."),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred. Please try again later.")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        // Attach Activity/Trace ID for production diagnostics
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        if (ex is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            problemDetails.Extensions["errors"] = errors;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, jsonOptions));
    }
}
