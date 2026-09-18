using EnrollmentDemo.Application.Features.Applications.Queries.GetApplicationDetail;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EnrollmentDemo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetApplicationDetailQuery>());
        services.AddValidatorsFromAssemblyContaining<GetApplicationDetailQueryValidator>();
        return services;
    }
}