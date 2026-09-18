using ProviderEnrollment.Api.Contracts;
using ProviderEnrollment.Api.Models;

namespace ProviderEnrollment.Api.Services;

public interface IReadinessEvaluationService
{
  ApplicationEvaluationDto Evaluate(EnrollmentApplication application);
}