namespace ProviderEnrollment.Api.Models;

public sealed class PayerRuleVersion
{
  public required string RuleVersionId { get; init; }
  public required string PayerId { get; init; }
  public required string PayerName { get; init; }

  /// <summary>
  /// Exact expected values: NEW_ENROLLMENT, RE_CREDENTIALING
  /// </summary>
  public required string ApplicationType { get; init; }

  /// <summary>
  /// Calendar date (YYYY-MM-DD) inclusive.
  /// </summary>
  public required string EffectiveFrom { get; init; }

  /// <summary>
  /// Calendar date (YYYY-MM-DD) exclusive, or null for open-ended.
  /// </summary>
  public string? EffectiveTo { get; init; }

  public required List<RequiredDocumentRule> RequiredDocuments { get; init; }
  public required List<RequiredFieldRule> RequiredFields { get; init; }
}

public sealed class RequiredDocumentRule
{
  public required string Key { get; init; }
  public required string Label { get; init; }
  public required string DocumentId { get; init; }

  /// <summary>
  /// If true, document must have an expiration date; missing expiration date is a blocking deficiency.
  /// </summary>
  public required bool RequiresExpirationDate { get; init; }
}

public sealed class RequiredFieldRule
{
  public required string Key { get; init; }
  public required string Label { get; init; }

  /// <summary>
  /// Field key in EnrollmentApplication.Fields
  /// </summary>
  public required string FieldKey { get; init; }
}