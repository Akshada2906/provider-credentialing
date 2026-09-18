using Microsoft.AspNetCore.Mvc;
using ProviderEnrollment.Api.Contracts;
using ProviderEnrollment.Api.Services;

namespace ProviderEnrollment.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class ApplicationsController : ControllerBase
{
  private readonly IEnrollmentDataService _data;
  private readonly IReadinessEvaluationService _evaluation;

  public ApplicationsController(IEnrollmentDataService data, IReadinessEvaluationService evaluation)
  {
    _data = data;
    _evaluation = evaluation;
  }

  [HttpGet]
  public ActionResult<ApplicationListResponseDto> GetApplications([FromQuery] ApplicationsQueryDto query)
  {
    var validationError = ValidateQuery(query);
    if (validationError is not null)
      return BadRequest(validationError);

    var activeApps = _data.Applications.Where(a => a.IsActive).ToList();

    // Evaluate active apps (for counts across all active apps before filtering)
    var evaluations = activeApps.Select(a => _evaluation.Evaluate(a)).ToList();

    var fullyEvaluated = evaluations.Where(e => e.OverallStatus is not null).ToList();
    var statusCounts = new ReadinessSummaryDto(
      ReadyToSubmit: fullyEvaluated.Count(e => e.OverallStatus == ReadinessStatuses.Payer.ReadyToSubmit),
      Incomplete: fullyEvaluated.Count(e => e.OverallStatus == ReadinessStatuses.Payer.Incomplete),
      ExpiringSoon: fullyEvaluated.Count(e => e.OverallStatus == ReadinessStatuses.Payer.ExpiringSoon)
    );
    var evaluationErrorCount = evaluations.Count(e => e.OverallStatus is null);

    // Apply filters to items
    IEnumerable<ApplicationEvaluationDto> filtered = evaluations;

    if (!string.IsNullOrWhiteSpace(query.Status))
    {
      filtered = filtered.Where(e => e.OverallStatus == query.Status);
    }

    if (!string.IsNullOrWhiteSpace(query.PayerId))
    {
      filtered = filtered.Where(e => e.Payers.Any(p => string.Equals(p.PayerId, query.PayerId, StringComparison.OrdinalIgnoreCase)));
    }

    if (!string.IsNullOrWhiteSpace(query.ApplicationType))
    {
      filtered = filtered.Where(e => string.Equals(e.ApplicationType, query.ApplicationType, StringComparison.OrdinalIgnoreCase));
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var term = query.Search.Trim();
      filtered = filtered.Where(e =>
        e.ProviderName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        e.Id.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    // Map to list items
    var items = filtered.Select(e => new ApplicationListItemDto(
      Id: e.Id,
      ProviderId: e.ProviderId,
      ProviderName: e.ProviderName,
      ApplicationType: e.ApplicationType,
      OverallStatus: e.OverallStatus,
      LastUpdated: e.LastUpdated,
      PayerCount: e.Payers.Count,
      HasEvaluationErrors: e.OverallStatus is null
    )).ToList();

    // Sorting
    items = ApplySort(items, query.SortBy, query.SortDirection);

    var response = new ApplicationListResponseDto(
      Items: items,
      StatusCounts: statusCounts,
      EvaluationErrorCount: evaluationErrorCount,
      ConfiguredEvaluationDate: _data.ConfiguredEvaluationDate
    );

    return Ok(response);
  }

  [HttpGet("{id}")]
  public ActionResult<ApplicationEvaluationDto> GetApplicationById([FromRoute] string id)
  {
    var app = _data.FindApplication(id);
    if (app is null)
      return NotFound(new ApiErrorDto("NOT_FOUND", "Application not found."));

    var eval = _evaluation.Evaluate(app);
    return Ok(eval);
  }

  private static ApiErrorDto? ValidateQuery(ApplicationsQueryDto query)
  {
    if (!string.IsNullOrWhiteSpace(query.Status) && !ReadinessStatuses.Payer.All.Contains(query.Status))
      return new ApiErrorDto("INVALID_QUERY", "Invalid 'status' query parameter.");

    if (!string.IsNullOrWhiteSpace(query.ApplicationType) &&
        !string.Equals(query.ApplicationType, "NEW_ENROLLMENT", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(query.ApplicationType, "RE_CREDENTIALING", StringComparison.OrdinalIgnoreCase))
      return new ApiErrorDto("INVALID_QUERY", "Invalid 'applicationType' query parameter.");

    if (!string.IsNullOrWhiteSpace(query.SortBy) &&
        !string.Equals(query.SortBy, "providerName", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(query.SortBy, "lastUpdated", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(query.SortBy, "readiness", StringComparison.OrdinalIgnoreCase))
      return new ApiErrorDto("INVALID_QUERY", "Invalid 'sortBy' query parameter.");

    if (!string.IsNullOrWhiteSpace(query.SortDirection) &&
        !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
      return new ApiErrorDto("INVALID_QUERY", "Invalid 'sortDirection' query parameter.");

    return null;
  }

  private static List<ApplicationListItemDto> ApplySort(
    List<ApplicationListItemDto> items,
    string? sortBy,
    string? sortDirection)
  {
    var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

    IOrderedEnumerable<ApplicationListItemDto> ordered = (sortBy ?? "providerName").ToLowerInvariant() switch
    {
      "lastupdated" => desc
        ? items.OrderByDescending(i => i.LastUpdated)
        : items.OrderBy(i => i.LastUpdated),

      "readiness" => desc
        ? items.OrderByDescending(i => ReadinessRank(i.OverallStatus)).ThenBy(i => i.ProviderName)
        : items.OrderBy(i => ReadinessRank(i.OverallStatus)).ThenBy(i => i.ProviderName),

      _ => desc
        ? items.OrderByDescending(i => i.ProviderName)
        : items.OrderBy(i => i.ProviderName)
    };

    return ordered.ToList();
  }

  private static int ReadinessRank(string? status)
  {
    // Sorting by readiness: Incomplete (worst) first, then Expiring Soon, then Ready to Submit, then errors last
    return status switch
    {
      ReadinessStatuses.Payer.Incomplete => 0,
      ReadinessStatuses.Payer.ExpiringSoon => 1,
      ReadinessStatuses.Payer.ReadyToSubmit => 2,
      null => 3,
      _ => 4
    };
  }
}