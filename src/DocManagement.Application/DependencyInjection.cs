using DocManagement.Application.Helpers;
using DocManagement.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<UrlHelper>();
        services.AddScoped<DocumentService>();
        services.AddScoped<PreviewService>();

        return services;
    }
}
