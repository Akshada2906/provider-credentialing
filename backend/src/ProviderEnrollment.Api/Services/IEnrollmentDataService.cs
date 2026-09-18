using ProviderEnrollment.Api.Models;

namespace ProviderEnrollment.Api.Services;

public interface IEnrollmentDataService
{
  IReadOnlyList<EnrollmentApplication> Applications { get; }
  IReadOnlyList<PayerRuleVersion> RuleVersions { get; }
  ReadinessConfig Config { get; }
  DateOnly ConfiguredEvaluationDate { get; }

  EnrollmentApplication? FindApplication(string id);
}