using System.ComponentModel.DataAnnotations;

namespace ProviderEnrollment.Api.Contracts;

public sealed record ReadinessSummaryDto(
  int ReadyToSubmit,
  int Incomplete,
  int ExpiringSoon
);

public sealed record ApplicationListItemDto(
  string Id,
  string ProviderId,
  string ProviderName,
  string ApplicationType,
  string? OverallStatus,
  DateTime LastUpdated,
  int PayerCount,
  bool HasEvaluationErrors
);

public sealed record ApplicationListResponseDto(
  IReadOnlyList<ApplicationListItemDto> Items,
  ReadinessSummaryDto StatusCounts,
  int EvaluationErrorCount,
  DateOnly ConfiguredEvaluationDate
);

public sealed record RequirementEvaluationDto(
  string Key,
  string Label,
  string Kind,
  string Status,
  string? Reason,
  string? RequiredAction,
  string? DocumentId,
  DateOnly? ExpiresOn,
  int? DaysUntilExpiration
);

public sealed record PayerEvaluationDto(
  string PayerId,
  string PayerName,
  string? Status,
  string? RuleVersionId,
  DateOnly? EffectiveFrom,
  DateOnly? EffectiveTo,
  IReadOnlyList<string> EvaluationErrors,
  IReadOnlyList<RequirementEvaluationDto> Requirements,
  int BlockingDeficiencyCount,
  int ExpirationWarningCount
);

public sealed record ApplicationEvaluationDto(
  string Id,
  string ProviderId,
  string ProviderName,
  string ApplicationType,
  bool IsActive,
  DateOnly? SubmittedOn,
  DateTime LastUpdated,
  string? OverallStatus,
  DateOnly EvaluationDate,
  string EvaluationDateSource,
  IReadOnlyList<string> EvaluationErrors,
  IReadOnlyList<PayerEvaluationDto> Payers
);

public sealed class ApplicationsQueryDto
{
  [FromQuery(Name = "status")]
  public string? Status { get; init; }

  [FromQuery(Name = "payerId")]
  public string? PayerId { get; init; }

  [FromQuery(Name = "applicationType")]
  public string? ApplicationType { get; init; }

  [FromQuery(Name = "search")]
  public string? Search { get; init; }

  [FromQuery(Name = "sortBy")]
  public string? SortBy { get; init; }

  [FromQuery(Name = "sortDirection")]
  public string? SortDirection { get; init; }
}

public sealed record ApiErrorDto(string Code, string Message);