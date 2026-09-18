namespace ProviderEnrollment.Api.Models;

public sealed class PayerRuleVersion
{
  public required string PayerId { get; init; }
  public required string PayerName { get; init; }
  public required string ApplicationType { get; init; } // NEW_ENROLLMENT | RE_CREDENTIALING

  public required string RuleVersionId { get; init; }

  public required DateOnly EffectiveFrom { get; init; }
  public DateOnly? EffectiveTo { get; init; } // [from, to)

  public required List<RequiredDocumentRule> RequiredDocuments { get; init; } = new();
  public required List<RequiredFieldRule> RequiredFields { get; init; } = new();
}

public sealed class RequiredDocumentRule
{
  public required string Key { get; init; }
  public required string Label { get; init; }

  public bool RequiresExpiration { get; init; } = true;
  public string? RequiredAction { get; init; }
}

public sealed class RequiredFieldRule
{
  public required string Key { get; init; }
  public required string Label { get; init; }
  public string? RequiredAction { get; init; }
}