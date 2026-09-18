using System.Text.Json;
using ProviderEnrollment.Api.Models;

namespace ProviderEnrollment.Api.Services;

public sealed class JsonEnrollmentDataService : IEnrollmentDataService
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
  {
    PropertyNameCaseInsensitive = true
  };

  public IReadOnlyList<EnrollmentApplication> Applications { get; }
  public IReadOnlyList<PayerRuleVersion> RuleVersions { get; }
  public ReadinessConfig Config { get; }
  public DateOnly ConfiguredEvaluationDate { get; }

  public JsonEnrollmentDataService(IConfiguration configuration, IWebHostEnvironment env)
  {
    var appsPath = configuration["Data:ApplicationsPath"] ?? "Data/applications.json";
    var rulesPath = configuration["Data:PayerRulesPath"] ?? "Data/payer-rules.json";
    var configPath = configuration["Data:ReadinessConfigPath"] ?? "Data/readiness-config.json";

    var appsFullPath = Path.Combine(env.ContentRootPath, appsPath);
    var rulesFullPath = Path.Combine(env.ContentRootPath, rulesPath);
    var configFullPath = Path.Combine(env.ContentRootPath, configPath);

    Config = ReadJsonRequired<ReadinessConfig>(configFullPath, "readiness-config.json");
    ConfiguredEvaluationDate = Config.FixedEvaluationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

    Applications = ReadJsonRequired<List<EnrollmentApplication>>(appsFullPath, "applications.json");
    RuleVersions = ReadJsonRequired<List<PayerRuleVersion>>(rulesFullPath, "payer-rules.json");

    Validate();
  }

  public EnrollmentApplication? FindApplication(string id) =>
    Applications.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

  private static T ReadJsonRequired<T>(string path, string logicalName)
  {
    try
    {
      if (!File.Exists(path))
        throw new InvalidOperationException($"Required data file missing: {logicalName}");

      var json = File.ReadAllText(path);
      var obj = JsonSerializer.Deserialize<T>(json, JsonOptions);
      if (obj is null)
        throw new InvalidOperationException($"Failed to deserialize required data file: {logicalName}");

      return obj;
    }
    catch (JsonException)
    {
      throw new InvalidOperationException($"Malformed JSON in required data file: {logicalName}");
    }
  }

  private void Validate()
  {
    if (Config.ExpirationThresholdDays <= 0)
      throw new InvalidOperationException("Invalid readiness configuration: expirationThresholdDays must be > 0.");

    // Applications
    var appIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var app in Applications)
    {
      if (string.IsNullOrWhiteSpace(app.Id))
        throw new InvalidOperationException("Invalid applications.json: application id is required.");
      if (!appIdSet.Add(app.Id))
        throw new InvalidOperationException($"Invalid applications.json: duplicate application id '{app.Id}'.");

      if (string.IsNullOrWhiteSpace(app.ProviderId) || string.IsNullOrWhiteSpace(app.ProviderName))
        throw new InvalidOperationException($"Invalid applications.json: providerId/providerName required for '{app.Id}'.");

      if (string.IsNullOrWhiteSpace(app.ApplicationType))
        throw new InvalidOperationException($"Invalid applications.json: applicationType required for '{app.Id}'.");

      if (app.TargetPayers is null || app.TargetPayers.Count == 0)
        throw new InvalidOperationException($"Invalid applications.json: targetPayers must be non-empty for '{app.Id}'.");

      var payerSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var p in app.TargetPayers)
      {
        if (string.IsNullOrWhiteSpace(p.PayerId))
          throw new InvalidOperationException($"Invalid applications.json: payerId required for '{app.Id}'.");
        if (!payerSet.Add(p.PayerId))
          throw new InvalidOperationException($"Invalid applications.json: duplicate payerId '{p.PayerId}' in '{app.Id}'.");
      }

      var docIdSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var d in app.Documents ?? [])
      {
        if (string.IsNullOrWhiteSpace(d.DocumentId))
          throw new InvalidOperationException($"Invalid applications.json: documentId required for '{app.Id}'.");
        if (!docIdSet.Add(d.DocumentId))
          throw new InvalidOperationException($"Invalid applications.json: duplicate documentId '{d.DocumentId}' in '{app.Id}'.");
        if (string.IsNullOrWhiteSpace(d.Key) || string.IsNullOrWhiteSpace(d.Label))
          throw new InvalidOperationException($"Invalid applications.json: document key/label required for '{app.Id}' doc '{d.DocumentId}'.");
      }
    }

    // Rules: at least 20 distinct payers
    var distinctPayers = RuleVersions.Select(r => r.PayerId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    if (distinctPayers < 20)
      throw new InvalidOperationException("Invalid payer-rules.json: must include at least 20 distinct payer IDs for the demo.");

    // Rules: validate effective intervals non-overlapping per payer+appType, and required keys unique
    var byPayerType = RuleVersions.GroupBy(r => (PayerId: r.PayerId.ToUpperInvariant(), r.ApplicationType.ToUpperInvariant()));
    foreach (var g in byPayerType)
    {
      var versions = g.OrderBy(v => v.EffectiveFrom).ToList();
      for (var i = 0; i < versions.Count; i++)
      {
        var v = versions[i];
        if (string.IsNullOrWhiteSpace(v.RuleVersionId))
          throw new InvalidOperationException("Invalid payer-rules.json: ruleVersionId is required.");
        if (string.IsNullOrWhiteSpace(v.PayerName))
          throw new InvalidOperationException($"Invalid payer-rules.json: payerName is required for payer '{v.PayerId}'.");

        if (v.EffectiveTo.HasValue && v.EffectiveTo.Value <= v.EffectiveFrom)
          throw new InvalidOperationException($"Invalid payer-rules.json: effectiveTo must be after effectiveFrom for ruleVersionId '{v.RuleVersionId}'.");

        var requiredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in v.RequiredDocuments ?? [])
        {
          if (string.IsNullOrWhiteSpace(d.Key) || string.IsNullOrWhiteSpace(d.Label))
            throw new InvalidOperationException($"Invalid payer-rules.json: required document key/label required for ruleVersionId '{v.RuleVersionId}'.");
          if (!requiredKeys.Add(d.Key))
            throw new InvalidOperationException($"Invalid payer-rules.json: duplicate requirement key '{d.Key}' in ruleVersionId '{v.RuleVersionId}'.");
        }
        foreach (var f in v.RequiredFields ?? [])
        {
          if (string.IsNullOrWhiteSpace(f.Key) || string.IsNullOrWhiteSpace(f.Label))
            throw new InvalidOperationException($"Invalid payer-rules.json: required field key/label required for ruleVersionId '{v.RuleVersionId}'.");
          if (!requiredKeys.Add(f.Key))
            throw new InvalidOperationException($"Invalid payer-rules.json: duplicate requirement key '{f.Key}' in ruleVersionId '{v.RuleVersionId}'.");
        }

        for (var j = i + 1; j < versions.Count; j++)
        {
          var other = versions[j];

          var vEnd = v.EffectiveTo ?? DateOnly.MaxValue;
          var oEnd = other.EffectiveTo ?? DateOnly.MaxValue;

          var overlap = v.EffectiveFrom < oEnd && other.EffectiveFrom < vEnd;
          if (overlap)
          {
            throw new InvalidOperationException(
              $"Invalid payer-rules.json: overlapping effective intervals for payer '{v.PayerId}', applicationType '{v.ApplicationType}'.");
          }
        }
      }
    }
  }
}