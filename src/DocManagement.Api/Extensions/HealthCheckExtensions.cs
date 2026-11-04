using DocManagement.Api.HealthChecks;
using DocManagement.Domain.Constants;
using DocManagement.Infrastructure.Messaging;
using DocManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocManagement.Api.Extensions;

internal static class HealthCheckExtensions
{
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var checks = services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(HealthCheckConstants.DatabaseCheckName);

        var messaging = configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>() ?? new MessagingOptions();
        if (messaging.Provider == MessagingProvider.RabbitMq)
        {
            checks.AddCheck<RabbitMqHealthCheck>(
                HealthCheckConstants.RabbitMqCheckName,
                timeout: TimeSpan.FromSeconds(HealthCheckConstants.TimeoutSeconds));
        }

        return services;
    }

    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(HealthCheckConstants.Path, new HealthCheckOptions { ResponseWriter = WriteResponseAsync })
            .AllowAnonymous()
            .DisableRateLimiting();

        return endpoints;
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        var response = new HealthResponse(
            report.Status.ToString(),
            report.TotalDuration,
            report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthEntryResponse(entry.Value.Status.ToString(), entry.Value.Duration, entry.Value.Description)));

        return context.Response.WriteAsJsonAsync(response, context.RequestAborted);
    }

    private sealed record HealthResponse(string Status, TimeSpan TotalDuration, IReadOnlyDictionary<string, HealthEntryResponse> Checks);

    private sealed record HealthEntryResponse(string Status, TimeSpan Duration, string? Description);
}
