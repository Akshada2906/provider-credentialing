using EnrollmentDemo.Domain.Enums;

namespace EnrollmentDemo.Application.Features.Applications.Queries.GetApplicationDetail;

public sealed record ApplicationEvaluationDto
{
    public required string Id { get; init; }
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required ApplicationType ApplicationType { get; init; }
    public required bool IsActive { get; init; }
    public DateTimeOffset? SubmittedOn { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }

    // Readiness / evaluation
    public string? OverallStatus { get; init; } // "Ready to Submit" | "Incomplete" | "Expiring Soon" | null
    public required DateTimeOffset EvaluationDate { get; init; }
    public required EvaluationDateSource EvaluationDateSource { get; init; }
    public required List<string> EvaluationErrors { get; init; } = new();

    public required List<PayerEvaluationDto> Payers { get; init; } = new();
}

public sealed record PayerEvaluationDto
{
    public required string PayerId { get; init; }
    public required string PayerName { get; init; }

    public string? Status { get; init; } // "Ready to Submit" | "Incomplete" | "Expiring Soon" | null

    public string? RuleVersionId { get; init; }
    public DateTimeOffset? EffectiveFrom { get; init; }
    public DateTimeOffset? EffectiveTo { get; init; }

    public required List<string> EvaluationErrors { get; init; } = new();
    public required List<RequirementEvaluationDto> Requirements { get; init; } = new();

    public required int BlockingDeficiencyCount { get; init; }
    public required int ExpirationWarningCount { get; init; }
}

public sealed record RequirementEvaluationDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required RequirementKind Kind { get; init; }

    public required string Status { get; init; } // "Present & Valid" | "Missing" | "Expired" | "Expiring Soon"
    public string? Reason { get; init; }
    public string? RequiredAction { get; init; }

    public string? DocumentId { get; init; }
    public DateTimeOffset? ExpiresOn { get; init; }
    public int? DaysUntilExpiration { get; init; }
}