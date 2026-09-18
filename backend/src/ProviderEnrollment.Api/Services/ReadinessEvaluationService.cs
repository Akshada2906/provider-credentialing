using ProviderEnrollment.Api.Contracts;
using ProviderEnrollment.Api.Models;

namespace ProviderEnrollment.Api.Services;

public sealed class ReadinessEvaluationService : IReadinessEvaluationService
{
  private readonly IEnrollmentDataService _data;

  public ReadinessEvaluationService(IEnrollmentDataService data)
  {
    _data = data;
  }

  public ApplicationEvaluationDto Evaluate(EnrollmentApplication application)
  {
    var (evaluationDate, evaluationDateSource) = ResolveEvaluationDate(application, _data.Config);

    var appErrors = new List<string>();
    var payerEvals = new List<PayerEvaluationDto>();

    foreach (var target in application.TargetPayers)
    {
      payerEvals.Add(EvaluatePayer(application, target.PayerId, evaluationDate, appErrors));
    }

    var overallStatus = ReadinessStatuses.CombineOverall(payerEvals.Select(p => p.Status).ToArray());

    // If any payer couldn't be evaluated, application has evaluation error and overallStatus must be null
    if (payerEvals.Any(p => p.Status is null))
    {
      if (!appErrors.Any())
        appErrors.Add("One or more payers could not be evaluated.");
      overallStatus = null;
    }

    return new ApplicationEvaluationDto(
      application.Id,
      application.ProviderId,
      application.ProviderName,
      application.ApplicationType,
      application.IsActive,
      application.SubmittedOn,
      application.LastUpdated,
      overallStatus,
      evaluationDate,
      evaluationDateSource,
      appErrors,
      payerEvals
    );
  }

  private PayerEvaluationDto EvaluatePayer(
    EnrollmentApplication application,
    string payerId,
    DateOnly evaluationDate,
    List<string> applicationErrorsCollector)
  {
    var errors = new List<string>();

    var matching = SelectRuleVersion(payerId, application.ApplicationType, evaluationDate);
    if (matching is null)
    {
      errors.Add("No matching payer rule version found for the evaluation date.");
      applicationErrorsCollector.Add($"Payer '{payerId}' could not be evaluated.");
      return new PayerEvaluationDto(
        payerId,
        PayerName: ResolvePayerName(payerId),
        Status: null,
        RuleVersionId: null,
        EffectiveFrom: null,
        EffectiveTo: null,
        EvaluationErrors: errors,
        Requirements: Array.Empty<RequirementEvaluationDto>(),
        BlockingDeficiencyCount: 0,
        ExpirationWarningCount: 0
      );
    }

    var requirements = new List<RequirementEvaluationDto>();
    var blocking = 0;
    var warnings = 0;

    // Documents
    foreach (var docRule in matching.RequiredDocuments)
    {
      var matchDoc = application.Documents.FirstOrDefault(d => string.Equals(d.Key, docRule.Key, StringComparison.OrdinalIgnoreCase));

      if (matchDoc is null)
      {
        blocking++;
        requirements.Add(new RequirementEvaluationDto(
          Key: docRule.Key,
          Label: docRule.Label,
          Kind: ReadinessStatuses.RequirementKind.Document,
          Status: ReadinessStatuses.Requirement.Missing,
          Reason: "Required document is missing.",
          RequiredAction: docRule.RequiredAction,
          DocumentId: null,
          ExpiresOn: null,
          DaysUntilExpiration: null
        ));
        continue;
      }

      if (!docRule.RequiresExpiration)
      {
        requirements.Add(new RequirementEvaluationDto(
          Key: docRule.Key,
          Label: docRule.Label,
          Kind: ReadinessStatuses.RequirementKind.Document,
          Status: ReadinessStatuses.Requirement.PresentAndValid,
          Reason: null,
          RequiredAction: docRule.RequiredAction,
          DocumentId: matchDoc.DocumentId,
          ExpiresOn: matchDoc.ExpiresOn,
          DaysUntilExpiration: null
        ));
        continue;
      }

      if (!matchDoc.ExpiresOn.HasValue)
      {
        blocking++;
        requirements.Add(new RequirementEvaluationDto(
          Key: docRule.Key,
          Label: docRule.Label,
          Kind: ReadinessStatuses.RequirementKind.Document,
          Status: ReadinessStatuses.Requirement.Missing,
          Reason: "Expiration date missing.",
          RequiredAction: docRule.RequiredAction,
          DocumentId: matchDoc.DocumentId,
          ExpiresOn: null,
          DaysUntilExpiration: null
        ));
        continue;
      }

      var expiresOn = matchDoc.ExpiresOn.Value;
      var daysUntil = expiresOn.DayNumber - evaluationDate.DayNumber;

      if (expiresOn < evaluationDate)
      {
        blocking++;
        requirements.Add(new RequirementEvaluationDto(
          Key: docRule.Key,
          Label: docRule.Label,
          Kind: ReadinessStatuses.RequirementKind.Document,
          Status: ReadinessStatuses.Requirement.Expired,
          Reason: "Document is expired.",
          RequiredAction: docRule.RequiredAction,
          DocumentId: matchDoc.DocumentId,
          ExpiresOn: expiresOn,
          DaysUntilExpiration: daysUntil
        ));
      }
      else if (daysUntil <= _data.Config.ExpirationThresholdDays)
      {
        warnings++;
        requirements.Add(new RequirementEvaluationDto(
          Key: docRule.Key,
          Label: docRule.Label,
          Kind: ReadinessStatuses.RequirementKind.Document,
          Status: ReadinessStatuses.Requirement.ExpiringSoon,
          Reason: "Document is within the expiration alert window.",
          RequiredAction: docRule.RequiredAction,
          DocumentId: matchDoc.DocumentId,
          ExpiresOn: expiresOn,
          DaysUntilExpiration: daysUntil
        ));
      }
      else
      {
        requirements.Add(new RequirementEvaluationDto(
          Key: docRule.Key,
          Label: docRule.Label,
          Kind: ReadinessStatuses.RequirementKind.Document,
          Status: ReadinessStatuses.Requirement.PresentAndValid,
          Reason: null,
          RequiredAction: docRule.RequiredAction,
          DocumentId: matchDoc.DocumentId,
          ExpiresOn: expiresOn,
          DaysUntilExpiration: daysUntil
        ));
      }
    }

    // Fields
    foreach (var fieldRule in matching.RequiredFields)
    {
      var hasKey = application.Fields.TryGetValue(fieldRule.Key, out var raw);
      var isPresent = hasKey && IsNonEmptyPrimitive(raw);

      if (!isPresent)
      {
        blocking++;
        requirements.Add(new RequirementEvaluationDto(
          Key: fieldRule.Key,
          Label: fieldRule.Label,
          Kind: ReadinessStatuses.RequirementKind.Field,
          Status: ReadinessStatuses.Requirement.Missing,
          Reason: "Required field is missing.",
          RequiredAction: fieldRule.RequiredAction,
          DocumentId: null,
          ExpiresOn: null,
          DaysUntilExpiration: null
        ));
      }
      else
      {
        requirements.Add(new RequirementEvaluationDto(
          Key: fieldRule.Key,
          Label: fieldRule.Label,
          Kind: ReadinessStatuses.RequirementKind.Field,
          Status: ReadinessStatuses.Requirement.PresentAndValid,
          Reason: null,
          RequiredAction: fieldRule.RequiredAction,
          DocumentId: null,
          ExpiresOn: null,
          DaysUntilExpiration: null
        ));
      }
    }

    var payerStatus = ResolvePayerStatus(blocking, warnings);

    return new PayerEvaluationDto(
      payerId,
      matching.PayerName,
      payerStatus,
      matching.RuleVersionId,
      matching.EffectiveFrom,
      matching.EffectiveTo,
      errors,
      requirements,
      blocking,
      warnings
    );
  }

  private PayerRuleVersion? SelectRuleVersion(string payerId, string applicationType, DateOnly evaluationDate)
  {
    var matches = _data.RuleVersions
      .Where(r =>
        string.Equals(r.PayerId, payerId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(r.ApplicationType, applicationType, StringComparison.OrdinalIgnoreCase) &&
        r.EffectiveFrom <= evaluationDate &&
        (r.EffectiveTo is null || evaluationDate < r.EffectiveTo.Value))
      .ToList();

    // Exactly one required; ambiguous must be treated as error (never silently Ready)
    if (matches.Count != 1)
      return null;

    return matches[0];
  }

  private static (DateOnly date, string source) ResolveEvaluationDate(EnrollmentApplication app, ReadinessConfig config)
  {
    if (app.SubmittedOn.HasValue)
      return (app.SubmittedOn.Value, ReadinessStatuses.EvaluationDateSource.SubmittedOn);

    if (config.FixedEvaluationDate.HasValue)
      return (config.FixedEvaluationDate.Value, ReadinessStatuses.EvaluationDateSource.Configured);

    return (DateOnly.FromDateTime(DateTime.UtcNow), ReadinessStatuses.EvaluationDateSource.Current);
  }

  private static string ResolvePayerStatus(int blockingDeficiencies, int expirationWarnings)
  {
    if (blockingDeficiencies > 0)
      return ReadinessStatuses.Payer.Incomplete;

    if (expirationWarnings > 0)
      return ReadinessStatuses.Payer.ExpiringSoon;

    return ReadinessStatuses.Payer.ReadyToSubmit;
  }

  private string ResolvePayerName(string payerId)
  {
    var any = _data.RuleVersions.FirstOrDefault(r => string.Equals(r.PayerId, payerId, StringComparison.OrdinalIgnoreCase));
    return any?.PayerName ?? payerId;
  }

  private static bool IsNonEmptyPrimitive(object? value)
  {
    if (value is null) return false;

    return value switch
    {
      string s => !string.IsNullOrWhiteSpace(s),
      bool _ => true,
      byte or sbyte or short or ushort or int or uint or long or ulong => true,
      float or double or decimal => true,
      DateTime dt => dt != default,
      _ => true // accept other JSON primitives/objects as "present" for this POC
    };
  }
}