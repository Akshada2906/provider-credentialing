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
    var appErrors = new List<ApiErrorDto>();

    var (evaluationDate, source) = ResolveEvaluationDate(application);
    var configuredDate = ResolveConfiguredDate();

    // Evaluate each payer independently
    var payers = new List<PayerEvaluationDto>();
    var anyPayerError = false;

    foreach (var payerId in application.TargetPayerIds)
    {
      var payerEval = EvaluatePayer(application, payerId, evaluationDate, configuredDate);
      payers.Add(payerEval);

      if (payerEval.Status is null)
      {
        anyPayerError = true;
        appErrors.Add(new ApiErrorDto("PAYER_EVALUATION_FAILED", $"Payer {payerId} could not be evaluated."));
      }
    }

    string? overall = null;
    if (!anyPayerError)
    {
      overall = AggregateOverallStatus(payers.Select(p => p.Status!).ToList());
    }

    return new ApplicationEvaluationDto(
      application.Id,
      application.ProviderId,
      application.ProviderName,
      application.ApplicationType,
      application.IsActive,
      application.SubmittedOn,
      application.LastUpdated,
      overall,
      IsoDate.FormatDateOnly(evaluationDate),
      source,
      appErrors,
      payers
    );
  }

  private (DateOnly date, string source) ResolveEvaluationDate(EnrollmentApplication application)
  {
    if (IsoDate.TryParseDateOnly(application.SubmittedOn, out var submitted))
    {
      return (submitted, ReadinessStatuses.EvaluationDateSource.SubmittedOn);
    }

    if (IsoDate.TryParseDateOnly(_data.Config.FixedEvaluationDate, out var fixedDate))
    {
      return (fixedDate, ReadinessStatuses.EvaluationDateSource.Configured);
    }

    return (DateOnly.FromDateTime(DateTime.UtcNow), ReadinessStatuses.EvaluationDateSource.UtcNow);
  }

  private DateOnly ResolveConfiguredDate()
  {
    if (IsoDate.TryParseDateOnly(_data.Config.FixedEvaluationDate, out var d))
    {
      return d;
    }

    return DateOnly.FromDateTime(DateTime.UtcNow);
  }

  private PayerEvaluationDto EvaluatePayer(EnrollmentApplication app, string payerId, DateOnly evaluationDate, DateOnly configuredDate)
  {
    // Validate payer ID against configured payers per contract
    if (!_data.IsKnownPayerId(payerId))
    {
      return new PayerEvaluationDto(
        payerId,
        payerId,
        null,
        null,
        null,
        null,
        new[] { new ApiErrorDto("UNKNOWN_PAYER", "Payer ID is not configured.") },
        Array.Empty<RequirementEvaluationDto>(),
        0,
        0
      );
    }

    var match = SelectRuleVersion(payerId, app.ApplicationType, evaluationDate);
    if (match is null)
    {
      return new PayerEvaluationDto(
        payerId,
        payerId,
        null,
        null,
        null,
        null,
        new[] { new ApiErrorDto("NO_EFFECTIVE_RULE", "No effective rule version matched the evaluation date.") },
        Array.Empty<RequirementEvaluationDto>(),
        0,
        0
      );
    }

    var requirements = new List<RequirementEvaluationDto>();
    var blocking = 0;
    var warnings = 0;

    var docsById = app.Documents.ToDictionary(d => d.DocumentId, StringComparer.OrdinalIgnoreCase);

    foreach (var reqDoc in match.RequiredDocuments)
    {
      if (!docsById.TryGetValue(reqDoc.DocumentId, out var doc))
      {
        blocking++;
        requirements.Add(new RequirementEvaluationDto(
          reqDoc.Key,
          reqDoc.Label,
          ReadinessStatuses.RequirementKind.Document,
          ReadinessStatuses.Requirement.Missing,
          "Document missing.",
          "Provide document.",
          reqDoc.DocumentId,
          null,
          null
        ));
        continue;
      }

      // RequiresExpirationDate governs missing expiresOn behavior
      if (reqDoc.RequiresExpirationDate)
      {
        if (!IsoDate.TryParseDateOnly(doc.ExpiresOn, out var exp))
        {
          blocking++;
          requirements.Add(new RequirementEvaluationDto(
            reqDoc.Key,
            reqDoc.Label,
            ReadinessStatuses.RequirementKind.Document,
            ReadinessStatuses.Requirement.Missing,
            "Expiration date missing.",
            "Provide expiration date.",
            reqDoc.DocumentId,
            null,
            null
          ));
          continue;
        }

        var (status, reason, daysUntil) = EvaluateExpiration(exp, evaluationDate, _data.Config.ExpirationThresholdDays);
        if (status == ReadinessStatuses.Requirement.Expired || status == ReadinessStatuses.Requirement.Missing)
        {
          blocking++;
        }
        else if (status == ReadinessStatuses.Requirement.ExpiringSoon)
        {
          warnings++;
        }

        requirements.Add(new RequirementEvaluationDto(
          reqDoc.Key,
          reqDoc.Label,
          ReadinessStatuses.RequirementKind.Document,
          status,
          reason,
          status == ReadinessStatuses.Requirement.Expired ? "Renew document." :
            status == ReadinessStatuses.Requirement.ExpiringSoon ? "Renew soon." : null,
          reqDoc.DocumentId,
          IsoDate.FormatDateOnly(exp),
          daysUntil
        ));
      }
      else
      {
        // Non-expiring document: presence means Present & Valid
        requirements.Add(new RequirementEvaluationDto(
          reqDoc.Key,
          reqDoc.Label,
          ReadinessStatuses.RequirementKind.Document,
          ReadinessStatuses.Requirement.PresentAndValid,
          null,
          null,
          reqDoc.DocumentId,
          null,
          null
        ));
      }
    }

    foreach (var reqField in match.RequiredFields)
    {
      var present = app.Fields.TryGetValue(reqField.FieldKey, out var val) && !IsEmptyJsonPrimitive(val);
      if (!present)
      {
        blocking++;
        requirements.Add(new RequirementEvaluationDto(
          reqField.Key,
          reqField.Label,
          ReadinessStatuses.RequirementKind.Field,
          ReadinessStatuses.Requirement.Missing,
          "Field missing or empty.",
          "Provide value.",
          null,
          null,
          null
        ));
      }
      else
      {
        requirements.Add(new RequirementEvaluationDto(
          reqField.Key,
          reqField.Label,
          ReadinessStatuses.RequirementKind.Field,
          ReadinessStatuses.Requirement.PresentAndValid,
          null,
          null,
          null,
          null,
          null
        ));
      }
    }

    var payerStatus =
      blocking > 0 ? ReadinessStatuses.Payer.Incomplete :
      warnings > 0 ? ReadinessStatuses.Payer.ExpiringSoon :
      ReadinessStatuses.Payer.ReadyToSubmit;

    return new PayerEvaluationDto(
      match.PayerId,
      match.PayerName,
      payerStatus,
      match.RuleVersionId,
      match.EffectiveFrom,
      match.EffectiveTo,
      Array.Empty<ApiErrorDto>(),
      requirements,
      blocking,
      warnings
    );
  }

  private PayerRuleVersion? SelectRuleVersion(string payerId, string applicationType, DateOnly evaluationDate)
  {
    var candidates = _data.RuleVersions
      .Where(r =>
        string.Equals(r.PayerId, payerId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(r.ApplicationType, applicationType, StringComparison.Ordinal))
      .ToList();

    if (candidates.Count == 0) return null;

    var matches = new List<PayerRuleVersion>();

    foreach (var r in candidates)
    {
      if (!IsoDate.TryParseDateOnly(r.EffectiveFrom, out var from)) continue;

      DateOnly? to = null;
      if (r.EffectiveTo is not null)
      {
        if (!IsoDate.TryParseDateOnly(r.EffectiveTo, out var toParsed)) continue;
        to = toParsed;
      }

      // validity interval is [from, to)
      var inRange = evaluationDate >= from && (to is null || evaluationDate < to.Value);
      if (inRange) matches.Add(r);
    }

    // Exactly one match required; ambiguity is treated as error => null
    if (matches.Count != 1) return null;

    return matches[0];
  }

  private static (string status, string? reason, int? daysUntilExpiration) EvaluateExpiration(DateOnly expiresOn, DateOnly evaluationDate, int thresholdDays)
  {
    // expiryDate < evaluationDate is Expired. Document remains valid through its expiry date.
    if (expiresOn < evaluationDate)
    {
      return (ReadinessStatuses.Requirement.Expired, "Document expired.", -1);
    }

    var daysUntil = expiresOn.DayNumber - evaluationDate.DayNumber;

    // Day 0 through day threshold inclusive is Expiring Soon
    if (daysUntil <= thresholdDays)
    {
      return (ReadinessStatuses.Requirement.ExpiringSoon, "Document expiring soon.", daysUntil);
    }

    return (ReadinessStatuses.Requirement.PresentAndValid, null, daysUntil);
  }

  private static string AggregateOverallStatus(IReadOnlyList<string> payerStatuses)
  {
    // Precedence: Incomplete > Expiring Soon > Ready to Submit
    if (payerStatuses.Any(s => s == ReadinessStatuses.Payer.Incomplete))
    {
      return ReadinessStatuses.Payer.Incomplete;
    }

    if (payerStatuses.Any(s => s == ReadinessStatuses.Payer.ExpiringSoon))
    {
      return ReadinessStatuses.Payer.ExpiringSoon;
    }

    return ReadinessStatuses.Payer.ReadyToSubmit;
  }

  private static bool IsEmptyJsonPrimitive(object? value)
  {
    if (value is null) return true;

    if (value is string s) return string.IsNullOrWhiteSpace(s);

    // For numbers/bools, presence is sufficient (including 0/false)
    return false;
  }
}