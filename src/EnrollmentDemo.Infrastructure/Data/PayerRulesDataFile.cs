using EnrollmentDemo.Domain.Enums;

namespace EnrollmentDemo.Infrastructure.Data;

public sealed class PayerRulesDataFile
{
    public required List<PayerRuleRecord> Payers { get; init; } = new();
}

public sealed class PayerRuleRecord
{
    public required string PayerId { get; init; }
    public required string PayerName { get; init; }

    public required List<PayerRuleVersionRecord> RuleVersions { get; init; } = new();
}

public sealed class PayerRuleVersionRecord
{
    public required string RuleVersionId { get; init; }
    public required ApplicationType ApplicationType { get; init; }

    public required DateTimeOffset EffectiveFrom { get; init; }
    public DateTimeOffset? EffectiveTo { get; init; }

    public required List<DocumentRequirementRuleRecord> RequiredDocuments { get; init; } = new();
    public required List<FieldRequirementRuleRecord> RequiredFields { get; init; } = new();
}

public sealed class DocumentRequirementRuleRecord
{
    public required string Key { get; init; }
    public required string Label { get; init; }

    /// <summary>
    /// Document ID expected to be present on the application.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// If true, ExpiresOn must be provided; missing expiration becomes blocking deficiency.
    /// </summary>
    public bool RequiresExpirationDate { get; init; } = true;

    public string? RenewalAction { get; init; }
}

public sealed class FieldRequirementRuleRecord
{
    public required string Key { get; init; }
    public required string Label { get; init; }
}