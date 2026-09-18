using ProviderEnrollment.Api.Models;

namespace ProviderEnrollment.Api.Services;

public interface IEnrollmentDataService
{
  ReadinessConfig Config { get; }
  IReadOnlyList<EnrollmentApplication> Applications { get; }
  IReadOnlyList<PayerRuleVersion> RuleVersions { get; }

  EnrollmentApplication? FindApplicationById(string id);

  bool IsKnownPayerId(string payerId);
}