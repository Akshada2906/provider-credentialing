using Microsoft.AspNetCore.Mvc;
using ProviderEnrollment.Api.Contracts;
using ProviderEnrollment.Api.Services;

namespace ProviderEnrollment.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class ApplicationsController : ControllerBase
{
  private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
  {
    ReadinessStatuses.Payer.ReadyToSubmit,
    ReadinessStatuses.Payer.Incomplete,
    ReadinessStatuses.Payer.ExpiringSoon
  };

  private static readonly HashSet<string> AllowedApplicationTypes = new(StringComparer.Ordinal)
  {
    "NEW_ENROLLMENT",
    "RE_CREDENTIALING"
  };

  private static readonly HashSet<string> AllowedSortBy = new(StringComparer.Ordinal)
  {
    "providerName",
    "lastUpdated",
    "readiness"
  };

  private static readonly HashSet<string> AllowedSortDirection = new(StringComparer.OrdinalIgnoreCase)
  {
    "asc",
    "desc"
  };

  private readonly IEnrollmentDataService _data;
  private readonly IReadinessEvaluationService _eval;

  public ApplicationsController(IEnrollmentDataService data, IReadinessEvaluationService eval)
  {
    _data = data;
    _eval = eval;
  }

  [HttpGet]
  public ActionResult<ApplicationListResponseDto> Get([FromQuery] ApplicationsQueryDto query)
  {
    ValidateQuery(query);

    var configuredEvalDate = _data.Config.FixedEvaluationDate;
    var configuredDateString = configuredEvalDate ?? DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    var activeApps = _data.Applications.Where(a => a.IsActive).ToList();
    var evaluated = activeApps.Select(a => _eval.Evaluate(a)).ToList();

    var fullyEvaluated = evaluated.Where(e => e.OverallStatus is not null).ToList();

    var counts = new ReadinessSummaryDto(
      fullyEvaluated.Count(e => e.OverallStatus == ReadinessStatuses.Payer.ReadyToSubmit),
      fullyEvaluated.Count(e => e.OverallStatus == ReadinessStatuses.Payer.Incomplete),
      fullyEvaluated.Count(e => e.OverallStatus == ReadinessStatuses.Payer.ExpiringSoon)
    );

    var evaluationErrorCount = evaluated.Count(e => e.EvaluationErrors.Count > 0 || e.OverallStatus is null);

    IEnumerable<ApplicationEvaluationDto> filtered = fullyEvaluated;

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
      filtered = filtered.Where(e => e.ApplicationType == query.ApplicationType);
    }

    if (!string.IsNullOrWhiteSpace(query.Search))
    {
      var needle = query.Search.Trim();
      filtered = filtered.Where(e =>
        e.ProviderName.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
        e.Id.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    filtered = ApplySorting(filtered, query.SortBy, query.SortDirection);

    var items = filtered.Select(e => new ApplicationListItemDto(
      e.Id,
      e.ProviderId,
      e.ProviderName,
      e.ApplicationType,
      e.OverallStatus,
      e.LastUpdated,
      e.Payers.Count,
      e.EvaluationErrors.Count > 0 || e.Payers.Any(p => p.Status is null)
    )).ToList();

    return Ok(new ApplicationListResponseDto(items, counts, evaluationErrorCount, configuredDateString));
  }

  [HttpGet("{id}")]
  public ActionResult<ApplicationEvaluationDto> GetById([FromRoute] string id)
  {
    if (string.IsNullOrWhiteSpace(id))
    {
      throw new BadRequestException("ID is required.");
    }

    var app = _data.FindApplicationById(id);
    if (app is null)
    {
      throw new NotFoundException();
    }

    return Ok(_eval.Evaluate(app));
  }

  private static IEnumerable<ApplicationEvaluationDto> ApplySorting(IEnumerable<ApplicationEvaluationDto> src, string? sortBy, string? sortDirection)
  {
    var dir = (sortDirection ?? "asc").ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(sortBy))
    {
      // Default: lastUpdated desc
      return src.OrderByDescending(x => x.LastUpdated);
    }

    var asc = dir == "asc";

    return sortBy switch
    {
      "providerName" => asc ? src.OrderBy(x => x.ProviderName) : src.OrderByDescending(x => x.ProviderName),
      "lastUpdated" => asc ? src.OrderBy(x => x.LastUpdated) : src.OrderByDescending(x => x.LastUpdated),
      "readiness" => asc ? src.OrderBy(x => SortKeyForReadiness(x.OverallStatus!)) : src.OrderByDescending(x => SortKeyForReadiness(x.OverallStatus!)),
      _ => src.OrderByDescending(x => x.LastUpdated)
    };

    static int SortKeyForReadiness(string status) =>
      status switch
      {
        ReadinessStatuses.Payer.Incomplete => 0,
        ReadinessStatuses.Payer.ExpiringSoon => 1,
        ReadinessStatuses.Payer.ReadyToSubmit => 2,
        _ => 3
      };
  }

  private void ValidateQuery(ApplicationsQueryDto query)
  {
    if (!string.IsNullOrWhiteSpace(query.Status) && !AllowedStatuses.Contains(query.Status))
    {
      throw new BadRequestException("Invalid status filter.");
    }

    if (!string.IsNullOrWhiteSpace(query.ApplicationType) && !AllowedApplicationTypes.Contains(query.ApplicationType))
    {
      throw new BadRequestException("Invalid applicationType.");
    }

    if (!string.IsNullOrWhiteSpace(query.PayerId) && !_data.IsKnownPayerId(query.PayerId))
    {
      throw new BadRequestException("Invalid payerId.");
    }

    if (!string.IsNullOrWhiteSpace(query.SortBy) && !AllowedSortBy.Contains(query.SortBy))
    {
      throw new BadRequestException("Invalid sortBy.");
    }

    if (!string.IsNullOrWhiteSpace(query.SortDirection) && !AllowedSortDirection.Contains(query.SortDirection))
    {
      throw new BadRequestException("Invalid sortDirection.");
    }
  }
}