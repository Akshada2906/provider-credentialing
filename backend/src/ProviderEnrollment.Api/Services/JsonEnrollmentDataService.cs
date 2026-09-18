using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProviderEnrollment.Api.Contracts;
using ProviderEnrollment.Api.Models;

namespace ProviderEnrollment.Api.Services;

public sealed class JsonDataOptions
{
  public required string BasePath { get; init; }
  public required string ApplicationsFile { get; init; }
  public required string PayerRulesFile { get; init; }
  public required string ReadinessConfigFile { get; init; }
}

public sealed class ConfigurationValidationException : Exception
{
  public ConfigurationValidationException(string message) : base(message) { }
}

public sealed class JsonEnrollmentDataService : IEnrollmentDataService
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  public ReadinessConfig Config { get; }
  public IReadOnlyList<EnrollmentApplication> Applications { get; }
  public IReadOnlyList<PayerRuleVersion> RuleVersions { get; }

  private readonly HashSet<string> _knownPayerIds;

  public JsonEnrollmentDataService(IHostEnvironment env, IOptions<JsonDataOptions> options)
  {
    var opt = options.Value ?? throw new ConfigurationValidationException("Data options missing.");

    var basePath = Path.IsPathRooted(opt.BasePath)
      ? opt.BasePath
      : Path.Combine(env.ContentRootPath, opt.BasePath);

    Config = LoadJson<ReadinessConfig>(Path.Combine(basePath, opt.ReadinessConfigFile), "readiness-config.json");
    ValidateConfig(Config);

    Applications = new ReadOnlyCollection<EnrollmentApplication>(
      LoadJson<List<EnrollmentApplication>>(Path.Combine(basePath, opt.ApplicationsFile), "applications.json")
    );

    RuleVersions = new ReadOnlyCollection<PayerRuleVersion>(
      LoadJson<List<PayerRuleVersion>>(Path.Combine(basePath, opt.PayerRulesFile), "payer-rules.json")
    );

    ValidateApplications(Applications);
    ValidateRuleVersions(RuleVersions);

    _knownPayerIds = RuleVersions.Select(r => r.PayerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
  }

  public EnrollmentApplication? FindApplicationById(string id) =>
    Applications.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

  public bool IsKnownPayerId(string payerId) => _knownPayerIds.Contains(payerId);

  private static T LoadJson<T>(string filePath, string displayName)
  {
    try
    {
      if (!File.Exists(filePath))
      {
        throw new ConfigurationValidationException($"Required data file not found: {displayName}");
      }

      var json = File.ReadAllText(filePath);
      var value = JsonSerializer.Deserialize<T>(json, JsonOptions);

      if (value is null)
      {
        throw new ConfigurationValidationException($"Failed to deserialize {displayName}.");
      }

      return value;
    }
    catch (JsonException)
    {
      throw new ConfigurationValidationException($"Malformed JSON in {displayName}.");
    }
    catch (IOException)
    {
      throw new ConfigurationValidationException($"Unable to read {displayName}.");
    }
  }

  private static void ValidateConfig(ReadinessConfig cfg)
  {
    if (cfg.ExpirationThresholdDays <= 0 || cfg.ExpirationThresholdDays > 3650)
    {
      throw new ConfigurationValidationException("ExpirationThresholdDays must be between 1 and 3650.");
    }

    if (cfg.FixedEvaluationDate is not null && !IsoDate.TryParseDateOnly(cfg.FixedEvaluationDate, out _))
    {
      throw new ConfigurationValidationException("FixedEvaluationDate must be YYYY-MM-DD when provided.");
    }
  }

  private static void ValidateApplications(IReadOnlyList<EnrollmentApplication> apps)
  {
    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var a in apps)
    {
      if (string.IsNullOrWhiteSpace(a.Id)) throw new ConfigurationValidationException("Application id is required.");
      if (!ids.Add(a.Id)) throw new ConfigurationValidationException($"Duplicate application id: {a.Id}");

      if (string.IsNullOrWhiteSpace(a.ProviderId)) throw new ConfigurationValidationException($"Application {a.Id}: providerId is required.");
      if (string.IsNullOrWhiteSpace(a.ProviderName)) throw new ConfigurationValidationException($"Application {a.Id}: providerName is required.");
      if (a.ApplicationType is not ("NEW_ENROLLMENT" or "RE_CREDENTIALING")) throw new ConfigurationValidationException($"Application {a.Id}: invalid applicationType.");
      if (a.TargetPayerIds is null || a.TargetPayerIds.Count == 0) throw new ConfigurationValidationException($"Application {a.Id}: targetPayerIds must be non-empty.");

      if (a.SubmittedOn is not null && !IsoDate.TryParseDateOnly(a.SubmittedOn, out _))
      {
        throw new ConfigurationValidationException($"Application {a.Id}: submittedOn must be YYYY-MM-DD when provided.");
      }

      foreach (var doc in a.Documents ?? new List<ApplicationDocumentMetadata>())
      {
        if (string.IsNullOrWhiteSpace(doc.DocumentId)) throw new ConfigurationValidationException($"Application {a.Id}: documentId is required.");
        if (string.IsNullOrWhiteSpace(doc.Label)) throw new ConfigurationValidationException($"Application {a.Id}: document label is required.");

        if (doc.ExpiresOn is not null && !IsoDate.TryParseDateOnly(doc.ExpiresOn, out _))
        {
          throw new ConfigurationValidationException($"Application {a.Id}: document {doc.DocumentId} expiresOn must be YYYY-MM-DD when provided.");
        }
      }
    }
  }

  private static void ValidateRuleVersions(IReadOnlyList<PayerRuleVersion> rules)
  {
    if (rules.Count < 20)
    {
      throw new ConfigurationValidationException("At least 20 payer rule versions/payers are required for the demo.");
    }

    var versionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var r in rules)
    {
      if (string.IsNullOrWhiteSpace(r.RuleVersionId)) throw new ConfigurationValidationException("ruleVersionId is required.");
      if (!versionIds.Add(r.RuleVersionId)) throw new ConfigurationValidationException($"Duplicate ruleVersionId: {r.RuleVersionId}");

      if (string.IsNullOrWhiteSpace(r.PayerId)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: payerId is required.");
      if (string.IsNullOrWhiteSpace(r.PayerName)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: payerName is required.");
      if (r.ApplicationType is not ("NEW_ENROLLMENT" or "RE_CREDENTIALING")) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: invalid applicationType.");
      if (!IsoDate.TryParseDateOnly(r.EffectiveFrom, out _)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: effectiveFrom must be YYYY-MM-DD.");

      if (r.EffectiveTo is not null && !IsoDate.TryParseDateOnly(r.EffectiveTo, out _))
      {
        throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: effectiveTo must be YYYY-MM-DD when provided.");
      }

      var docKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var d in r.RequiredDocuments ?? new List<RequiredDocumentRule>())
      {
        if (string.IsNullOrWhiteSpace(d.Key)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: required document key is required.");
        if (!docKeys.Add(d.Key)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: duplicate required document key: {d.Key}");
        if (string.IsNullOrWhiteSpace(d.Label)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: required document label is required.");
        if (string.IsNullOrWhiteSpace(d.DocumentId)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: required document documentId is required.");
      }

      var fieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var f in r.RequiredFields ?? new List<RequiredFieldRule>())
      {
        if (string.IsNullOrWhiteSpace(f.Key)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: required field key is required.");
        if (!fieldKeys.Add(f.Key)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: duplicate required field key: {f.Key}");
        if (string.IsNullOrWhiteSpace(f.Label)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: required field label is required.");
        if (string.IsNullOrWhiteSpace(f.FieldKey)) throw new ConfigurationValidationException($"Rule {r.RuleVersionId}: required field fieldKey is required.");
      }
    }

    // Overlap validation per payer+applicationType
    var byGroup = rules.GroupBy(r => (r.PayerId.ToLowerInvariant(), r.ApplicationType));
    foreach (var g in byGroup)
    {
      var versions = g.Select(v => new
      {
        v.RuleVersionId,
        From = ParseDate(v.EffectiveFrom, $"Rule {v.RuleVersionId}: effectiveFrom invalid."),
        To = v.EffectiveTo is null ? (DateOnly?)null : ParseDate(v.EffectiveTo, $"Rule {v.RuleVersionId}: effectiveTo invalid.")
      }).OrderBy(x => x.From).ToList();

      for (var i = 0; i < versions.Count; i++)
      {
        var current = versions[i];
        if (current.To is not null && current.To.Value < current.From)
        {
          throw new ConfigurationValidationException($"Rule {current.RuleVersionId}: effectiveTo must be after effectiveFrom.");
        }

        if (i == 0) continue;
        var prev = versions[i - 1];

        // interval is [from, to), null to means infinity
        var prevTo = prev.To ?? DateOnly.MaxValue;
        if (current.From < prevTo)
        {
          throw new ConfigurationValidationException($"Overlapping rule versions for payer {g.Key.Item1} and type {g.Key.ApplicationType}.");
        }
      }
    }

    static DateOnly ParseDate(string input, string error)
    {
      if (!IsoDate.TryParseDateOnly(input, out var d)) throw new ConfigurationValidationException(error);
      return d;
    }
  }
}