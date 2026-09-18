namespace ProviderEnrollment.Api.Models;

public sealed class ReadinessConfig
{
  public required int ExpirationThresholdDays { get; init; }

  /// <summary>
  /// Fixed demo evaluation date (YYYY-MM-DD), optional.
  /// </summary>
  public string? FixedEvaluationDate { get; init; }
}