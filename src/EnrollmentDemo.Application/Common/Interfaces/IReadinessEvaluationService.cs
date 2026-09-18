using EnrollmentDemo.Application.Features.Applications.Queries.GetApplicationDetail;

namespace EnrollmentDemo.Application.Common.Interfaces;

public interface IReadinessEvaluationService
{
    /// <summary>
    /// Evaluates an application using backend-owned JSON fixtures and payer rule versions.
    /// Returns null when the application ID does not exist.
    /// </summary>
    Task<ApplicationEvaluationDto?> EvaluateApplicationAsync(string applicationId, CancellationToken cancellationToken);
}