using System.Text.Json.Serialization;

namespace ProviderEnrollment.Api.Contracts;

public sealed record ApiErrorDto(string Code, string Message);

public sealed record ReadinessSummaryDto(int ReadyToSubmit, int Incomplete, int ExpiringSoon);

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
  string ConfiguredEvaluationDate
);

public sealed record RequirementEvaluationDto(
  string Key,
  string Label,
  string Kind,
  string Status,
  string? Reason,
  string? RequiredAction,
  string? DocumentId,
  string? ExpiresOn,
  int? DaysUntilExpiration
);

public sealed record PayerEvaluationDto(
  string PayerId,
  string PayerName,
  string? Status,
  string? RuleVersionId,
  string? EffectiveFrom,
  string? EffectiveTo,
  IReadOnlyList<ApiErrorDto> EvaluationErrors,
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
  string? SubmittedOn,
  DateTime LastUpdated,
  string? OverallStatus,
  string EvaluationDate,
  string EvaluationDateSource,
  IReadOnlyList<ApiErrorDto> EvaluationErrors,
  IReadOnlyList<PayerEvaluationDto> Payers
);

public sealed record ApplicationsQueryDto(
  string? Status,
  string? PayerId,
  string? ApplicationType,
  string? Search,
  string? SortBy,
  string? SortDirection
);

public static class IsoDate
{
  public static string FormatDateOnly(DateOnly date) => date.ToString("yyyy-MM-dd");
  public static string? FormatDateOnly(DateOnly? date) => date is null ? null : date.Value.ToString("yyyy-MM-dd");

  public static bool TryParseDateOnly(string? input, out DateOnly date)
  {
    if (string.IsNullOrWhiteSpace(input))
    {
      date = default;
      return false;
    }

    return DateOnly.TryParseExact(input, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
  }
}