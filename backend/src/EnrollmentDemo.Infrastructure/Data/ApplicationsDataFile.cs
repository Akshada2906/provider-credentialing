using EnrollmentDemo.Domain.Enums;

namespace EnrollmentDemo.Infrastructure.Data;

public sealed class ApplicationsDataFile
{
    public required List<ApplicationRecord> Applications { get; init; } = new();
}

public sealed class ApplicationRecord
{
    public required string Id { get; init; }
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required ApplicationType ApplicationType { get; init; }
    public required bool IsActive { get; init; }
    public DateTimeOffset? SubmittedOn { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }

    public required List<PayerTargetRecord> PayerTargets { get; init; } = new();

    /// <summary>
    /// Map of required data field values present on the application.
    /// Missing or empty string counts as Missing at evaluation.
    /// </summary>
    public required Dictionary<string, string?> DataFields { get; init; } = new();

    /// <summary>
    /// Provider documents available for evaluation.
    /// key = documentId; expiration date optional depending on rule.
    /// </summary>
    public required List<DocumentRecord> Documents { get; init; } = new();
}

public sealed class PayerTargetRecord
{
    public required string PayerId { get; init; }
    public required string PayerName { get; init; }
}

public sealed class DocumentRecord
{
    public required string DocumentId { get; init; }
    public required string DocumentName { get; init; }
    public DateTimeOffset? ExpiresOn { get; init; }
}