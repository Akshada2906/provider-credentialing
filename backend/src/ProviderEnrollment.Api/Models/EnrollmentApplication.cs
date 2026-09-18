namespace ProviderEnrollment.Api.Models;

public sealed class EnrollmentApplication
{
  public required string Id { get; init; }
  public required string ProviderId { get; init; }
  public required string ProviderName { get; init; }
  public required string ApplicationType { get; init; } // NEW_ENROLLMENT | RE_CREDENTIALING
  public required bool IsActive { get; init; }
  public DateOnly? SubmittedOn { get; init; }
  public required DateTime LastUpdated { get; init; } // ISO UTC in JSON

  public required List<TargetPayer> TargetPayers { get; init; } = new();

  // Document metadata for this application (no uploads in POC)
  public required List<ApplicationDocument> Documents { get; init; } = new();

  // Arbitrary field values; evaluation checks key presence + non-empty semantics
  public required Dictionary<string, object?> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TargetPayer
{
  public required string PayerId { get; init; }
}

public sealed class ApplicationDocument
{
  public required string DocumentId { get; init; }
  public required string Key { get; init; } // matches rule requirement key
  public required string Label { get; init; }

  // Calendar date, when present
  public DateOnly? ExpiresOn { get; init; }
}