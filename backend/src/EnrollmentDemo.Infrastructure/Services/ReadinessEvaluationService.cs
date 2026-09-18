using EnrollmentDemo.Application.Common.Interfaces;
using EnrollmentDemo.Application.Features.Applications.Queries.GetApplicationDetail;
using EnrollmentDemo.Domain.Enums;
using EnrollmentDemo.Infrastructure.Data;
using EnrollmentDemo.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace EnrollmentDemo.Infrastructure.Services;

public sealed class ReadinessEvaluationService : IReadinessEvaluationService
{
    private readonly JsonDataStore _store;
    private readonly IOptions<ReadinessConfigOptions> _options;

    public ReadinessEvaluationService(JsonDataStore store, IOptions<ReadinessConfigOptions> options)
    {
        _store = store;
        _options = options;
    }

    public Task<ApplicationEvaluationDto?> EvaluateApplicationAsync(string applicationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var app = _store.Current.Applications.Applications.FirstOrDefault(a => string.Equals(a.Id, applicationId, StringComparison.OrdinalIgnoreCase));
        if (app is null)
        {
            return Task.FromResult<ApplicationEvaluationDto?>(null);
        }

        var (evaluationDate, dateSource) = ResolveEvaluationDate(app);
        var expirationWindowDays = ResolveExpirationWindowDays();

        var appErrors = new List<string>();
        var payerEvals = new List<PayerEvaluationDto>();

        foreach (var payer in app.PayerTargets)
        {
            payerEvals.Add(EvaluatePayer(app, payer, evaluationDate, dateSource, expirationWindowDays, appErrors));
        }

        // Overall status precedence: Incomplete > Expiring Soon > Ready to Submit
        // Exclude payers with evaluation errors (status null) from readiness calculation.
        var statuses = payerEvals
            .Where(p => p.Status is not null)
            .Select(p => p.Status!)
            .ToList();

        string? overallStatus = null;
        if (statuses.Count > 0)
        {
            if (statuses.Contains(ToReadinessString(ReadinessStatus.Incomplete)))
                overallStatus = ToReadinessString(ReadinessStatus.Incomplete);
            else if (statuses.Contains(ToReadinessString(ReadinessStatus.ExpiringSoon)))
                overallStatus = ToReadinessString(ReadinessStatus.ExpiringSoon);
            else
                overallStatus = ToReadinessString(ReadinessStatus.ReadyToSubmit);
        }

        var dto = new ApplicationEvaluationDto
        {
            Id = app.Id,
            ProviderId = app.ProviderId,
            ProviderName = app.ProviderName,
            ApplicationType = app.ApplicationType,
            IsActive = app.IsActive,
            SubmittedOn = app.SubmittedOn,
            LastUpdated = app.LastUpdated,
            OverallStatus = overallStatus,
            EvaluationDate = evaluationDate,
            EvaluationDateSource = dateSource,
            EvaluationErrors = appErrors,
            Payers = payerEvals
        };

        return Task.FromResult<ApplicationEvaluationDto?>(dto);
    }

    private PayerEvaluationDto EvaluatePayer(
        ApplicationRecord app,
        PayerTargetRecord payer,
        DateTimeOffset evaluationDate,
        EvaluationDateSource dateSource,
        int expirationWindowDays,
        List<string> appErrors)
    {
        var payerErrors = new List<string>();

        var payerRule = _store.Current.PayerRules.Payers.FirstOrDefault(p => string.Equals(p.PayerId, payer.PayerId, StringComparison.OrdinalIgnoreCase));
        if (payerRule is null)
        {
            payerErrors.Add("No matching rule for payer.");
            appErrors.Add($"Payer {payer.PayerId}: no matching rule configuration.");
            return new PayerEvaluationDto
            {
                PayerId = payer.PayerId,
                PayerName = payer.PayerName,
                Status = null,
                RuleVersionId = null,
                EffectiveFrom = null,
                EffectiveTo = null,
                EvaluationErrors = payerErrors,
                Requirements = new List<RequirementEvaluationDto>(),
                BlockingDeficiencyCount = 0,
                ExpirationWarningCount = 0
            };
        }

        var matching = payerRule.RuleVersions
            .Where(v => v.ApplicationType == app.ApplicationType)
            .Where(v => IsWithinEffectiveWindow(v.EffectiveFrom, v.EffectiveTo, evaluationDate))
            .ToList();

        if (matching.Count != 1)
        {
            payerErrors.Add("Rule version configuration ambiguous for evaluation date.");
            appErrors.Add($"Payer {payer.PayerId}: expected exactly one rule version match, found {matching.Count}.");
            return new PayerEvaluationDto
            {
                PayerId = payer.PayerId,
                PayerName = payer.PayerName,
                Status = null,
                RuleVersionId = null,
                EffectiveFrom = null,
                EffectiveTo = null,
                EvaluationErrors = payerErrors,
                Requirements = new List<RequirementEvaluationDto>(),
                BlockingDeficiencyCount = 0,
                ExpirationWarningCount = 0
            };
        }

        var version = matching[0];

        var requirements = new List<RequirementEvaluationDto>();

        var blocking = 0;
        var warnings = 0;

        // Documents
        foreach (var docRule in version.RequiredDocuments)
        {
            var doc = app.Documents.FirstOrDefault(d => string.Equals(d.DocumentId, docRule.DocumentId, StringComparison.OrdinalIgnoreCase));

            if (doc is null)
            {
                blocking++;
                requirements.Add(new RequirementEvaluationDto
                {
                    Key = docRule.Key,
                    Label = docRule.Label,
                    Kind = RequirementKind.DOCUMENT,
                    Status = ToRequirementString(RequirementStatus.Missing),
                    Reason = "Document not provided.",
                    RequiredAction = docRule.RenewalAction ?? "Provide document.",
                    DocumentId = docRule.DocumentId,
                    ExpiresOn = null,
                    DaysUntilExpiration = null
                });
                continue;
            }

            if (docRule.RequiresExpirationDate && doc.ExpiresOn is null)
            {
                blocking++;
                requirements.Add(new RequirementEvaluationDto
                {
                    Key = docRule.Key,
                    Label = docRule.Label,
                    Kind = RequirementKind.DOCUMENT,
                    Status = ToRequirementString(RequirementStatus.Missing),
                    Reason = "Expiration date missing.",
                    RequiredAction = docRule.RenewalAction ?? "Provide expiration date.",
                    DocumentId = doc.DocumentId,
                    ExpiresOn = null,
                    DaysUntilExpiration = null
                });
                continue;
            }

            if (doc.ExpiresOn is null)
            {
                // Not required to have expiration
                requirements.Add(new RequirementEvaluationDto
                {
                    Key = docRule.Key,
                    Label = docRule.Label,
                    Kind = RequirementKind.DOCUMENT,
                    Status = ToRequirementString(RequirementStatus.PresentAndValid),
                    Reason = null,
                    RequiredAction = null,
                    DocumentId = doc.DocumentId,
                    ExpiresOn = null,
                    DaysUntilExpiration = null
                });
                continue;
            }

            var (reqStatus, daysUntil) = EvaluateExpirationStatus(doc.ExpiresOn.Value, evaluationDate, expirationWindowDays);

            if (reqStatus == RequirementStatus.Expired)
            {
                blocking++;
                requirements.Add(new RequirementEvaluationDto
                {
                    Key = docRule.Key,
                    Label = docRule.Label,
                    Kind = RequirementKind.DOCUMENT,
                    Status = ToRequirementString(RequirementStatus.Expired),
                    Reason = "Document expired.",
                    RequiredAction = docRule.RenewalAction ?? "Renew document.",
                    DocumentId = doc.DocumentId,
                    ExpiresOn = doc.ExpiresOn,
                    DaysUntilExpiration = daysUntil
                });
            }
            else if (reqStatus == RequirementStatus.ExpiringSoon)
            {
                warnings++;
                requirements.Add(new RequirementEvaluationDto
                {
                    Key = docRule.Key,
                    Label = docRule.Label,
                    Kind = RequirementKind.DOCUMENT,
                    Status = ToRequirementString(RequirementStatus.ExpiringSoon),
                    Reason = "Document expiring soon.",
                    RequiredAction = docRule.RenewalAction ?? "Renew document.",
                    DocumentId = doc.DocumentId,
                    ExpiresOn = doc.ExpiresOn,
                    DaysUntilExpiration = daysUntil
                });
            }
            else
            {
                requirements.Add(new RequirementEvaluationDto
                {
                    Key = docRule.Key,
                    Label = docRule.Label,
                    Kind = RequirementKind.DOCUMENT,
                    Status = ToRequirementString(RequirementStatus.PresentAndValid),
                    Reason = null,
                    RequiredAction = null,
                    DocumentId = doc.DocumentId,
                    ExpiresOn = doc.ExpiresOn,
                    DaysUntilExpiration = daysUntil
                });
            }
        }

        // Fields
        foreach (var fieldRule in version.RequiredFields)
        {
            app.DataFields.TryGetValue(fieldRule.Key, out var value);

            if (string.IsNullOrWhiteSpace(value))
            {
                blocking++;
                requirements.Add(new RequirementEvaluationDto
                {
                    Key = fieldRule.Key,
                    Label = fieldRule.Label,
                    Kind = RequirementKind.FIELD,
                    Status = ToRequirementString(RequirementStatus.Missing),
                    Reason = "Required field is empty.",
                    RequiredAction = "Provide required value.",
                    DocumentId = null,
                    ExpiresOn = null,
                    DaysUntilExpiration = null
                });
            }
            else
            {
                requirements.Add(new RequirementEvaluationDto
                {
                    Key = fieldRule.Key,
                    Label = fieldRule.Label,
                    Kind = RequirementKind.FIELD,
                    Status = ToRequirementString(RequirementStatus.PresentAndValid),
                    Reason = null,
                    RequiredAction = null,
                    DocumentId = null,
                    ExpiresOn = null,
                    DaysUntilExpiration = null
                });
            }
        }

        string status;
        if (blocking > 0)
            status = ToReadinessString(ReadinessStatus.Incomplete);
        else if (warnings > 0)
            status = ToReadinessString(ReadinessStatus.ExpiringSoon);
        else
            status = ToReadinessString(ReadinessStatus.ReadyToSubmit);

        return new PayerEvaluationDto
        {
            PayerId = payer.PayerId,
            PayerName = payer.PayerName,
            Status = status,
            RuleVersionId = version.RuleVersionId,
            EffectiveFrom = version.EffectiveFrom,
            EffectiveTo = version.EffectiveTo,
            EvaluationErrors = payerErrors,
            Requirements = requirements,
            BlockingDeficiencyCount = blocking,
            ExpirationWarningCount = warnings
        };
    }

    private (DateTimeOffset EvaluationDate, EvaluationDateSource Source) ResolveEvaluationDate(ApplicationRecord app)
    {
        if (app.SubmittedOn is not null)
            return (TruncateToUtcDate(app.SubmittedOn.Value), EvaluationDateSource.SUBMITTED_ON);

        var configured = _options.Value.ConfiguredEvaluationDateUtc;
        if (configured is not null)
            return (TruncateToUtcDate(configured.Value), EvaluationDateSource.CONFIGURED);

        // fallback to backend JSON config file, then current
        var cfg = _store.Current.Config.ConfiguredEvaluationDateUtc;
        if (cfg is not null)
            return (TruncateToUtcDate(cfg.Value), EvaluationDateSource.CONFIGURED);

        return (TruncateToUtcDate(DateTimeOffset.UtcNow), EvaluationDateSource.CURRENT);
    }

    private int ResolveExpirationWindowDays()
    {
        var fromOptions = _options.Value.ExpirationWindowDays;
        if (fromOptions > 0) return fromOptions;

        var fromFile = _store.Current.Config.ExpirationWindowDays;
        if (fromFile > 0) return fromFile;

        return 90;
    }

    private static bool IsWithinEffectiveWindow(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, DateTimeOffset evaluationDate)
        => evaluationDate >= effectiveFrom && (effectiveTo is null || evaluationDate < effectiveTo.Value);

    private static (RequirementStatus Status, int? DaysUntil) EvaluateExpirationStatus(DateTimeOffset expiresOn, DateTimeOffset evaluationDate, int expirationWindowDays)
    {
        // Compare calendar dates in UTC: expiryDate < evaluationDate => Expired.
        // Day 0..N inclusive => Expiring Soon. Day N+1+ => Present & Valid.
        var expDate = expiresOn.UtcDateTime.Date;
        var evalDate = evaluationDate.UtcDateTime.Date;

        var daysUntil = (expDate - evalDate).Days;

        if (expDate < evalDate)
            return (RequirementStatus.Expired, daysUntil);

        if (daysUntil <= expirationWindowDays)
            return (RequirementStatus.ExpiringSoon, daysUntil);

        return (RequirementStatus.PresentAndValid, daysUntil);
    }

    private static DateTimeOffset TruncateToUtcDate(DateTimeOffset dt)
    {
        var d = dt.UtcDateTime.Date;
        return new DateTimeOffset(d, TimeSpan.Zero);
    }

    private static string ToReadinessString(ReadinessStatus status) =>
        status switch
        {
            ReadinessStatus.ReadyToSubmit => "Ready to Submit",
            ReadinessStatus.Incomplete => "Incomplete",
            ReadinessStatus.ExpiringSoon => "Expiring Soon",
            _ => "Incomplete"
        };

    private static string ToRequirementString(RequirementStatus status) =>
        status switch
        {
            RequirementStatus.PresentAndValid => "Present & Valid",
            RequirementStatus.Missing => "Missing",
            RequirementStatus.Expired => "Expired",
            RequirementStatus.ExpiringSoon => "Expiring Soon",
            _ => "Missing"
        };
}