using EnrollmentDemo.Application.Common.Interfaces;
using EnrollmentDemo.Infrastructure.Options;
using EnrollmentDemo.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnrollmentDemo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReadinessConfigOptions>()
            .Bind(configuration.GetSection(ReadinessConfigOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<JsonDataStore>();
        services.AddSingleton<IReadinessEvaluationService, ReadinessEvaluationService>();

        return services;
    }
}