using System.Globalization;
using System.Threading.RateLimiting;
using DocManagement.Domain.Constants;

namespace DocManagement.Api.Extensions;

internal static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var global = configuration.GetSection(RateLimitingConstants.GlobalSection).Get<TokenBucketRateLimiterOptions>()!;
        var previews = configuration.GetSection(RateLimitingConstants.PreviewsSection).Get<FixedWindowRateLimiterOptions>()!;

        return services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetTokenBucketLimiter(PartitionKey(context), _ => global));

            limiter.AddPolicy(RateLimitingConstants.PreviewsPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => previews));
        });
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
}
