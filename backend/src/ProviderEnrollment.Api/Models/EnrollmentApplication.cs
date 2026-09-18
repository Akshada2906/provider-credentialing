namespace ProviderEnrollment.Api.Models;

public sealed class EnrollmentApplication
{
  public required string Id { get; init; }
  public required string ProviderId { get; init; }
  public required string ProviderName { get; init; }

  /// <summary>
  /// Exact expected values: NEW_ENROLLMENT, RE_CREDENTIALING
  /// </summary>
  public required string ApplicationType { get; init; }

  public required bool IsActive { get; init; }

  /// <summary>
  /// Calendar date (YYYY-MM-DD) or null.
  /// Used as evaluation date basis when present.
  /// </summary>
  public string? SubmittedOn { get; init; }

  /// <summary>
  /// ISO UTC timestamp.
  /// </summary>
  public required DateTime LastUpdated { get; init; }

  /// <summary>
  /// Target payer IDs.
  /// </summary>
  public required List<string> TargetPayerIds { get; init; }

  /// <summary>
  /// Arbitrary application field values, treated as JSON primitives (string/number/bool/null).
  /// </summary>
  public required Dictionary<string, object?> Fields { get; init; }

  /// <summary>
  /// Document metadata keyed by documentId.
  /// </summary>
  public required List<ApplicationDocumentMetadata> Documents { get; init; }
}

public sealed class ApplicationDocumentMetadata
{
  public required string DocumentId { get; init; }
  public required string Label { get; init; }

  /// <summary>
  /// Calendar date (YYYY-MM-DD) or null.
  /// </summary>
  public string? ExpiresOn { get; init; }
}