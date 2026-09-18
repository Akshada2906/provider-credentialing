namespace ProviderEnrollment.Api.Models;

public sealed class ReadinessConfig
{
  public int ExpirationThresholdDays { get; init; } = 90;
  public DateOnly? FixedEvaluationDate { get; init; } // 2026-09-18 for repeatable demo
}