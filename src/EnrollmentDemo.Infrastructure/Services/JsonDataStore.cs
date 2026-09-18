using System.Text.Json;
using EnrollmentDemo.Infrastructure.Data;
using Microsoft.Extensions.Hosting;

namespace EnrollmentDemo.Infrastructure.Services;

public sealed class JsonDataStore
{
    private readonly Lazy<Snapshot> _snapshot;

    public JsonDataStore(IHostEnvironment env)
    {
        _snapshot = new Lazy<Snapshot>(() => Load(env.ContentRootPath), isThreadSafe: true);
    }

    public Snapshot Current => _snapshot.Value;

    private static Snapshot Load(string contentRootPath)
    {
        try
        {
            var dataDir = Path.Combine(contentRootPath, "Data");

            var appsPath = Path.Combine(dataDir, "applications.json");
            var rulesPath = Path.Combine(dataDir, "payer-rules.json");
            var cfgPath = Path.Combine(dataDir, "readiness-config.json");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var apps = DeserializeRequired<ApplicationsDataFile>(appsPath, jsonOptions);
            var rules = DeserializeRequired<PayerRulesDataFile>(rulesPath, jsonOptions);
            var cfg = DeserializeRequired<ReadinessConfigFile>(cfgPath, jsonOptions);

            Validate(apps, rules, cfg);

            return new Snapshot(apps, rules, cfg);
        }
        catch (JsonDataStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonDataStoreException($"Failed to load JSON data store: {ex.Message}", ex);
        }
    }

    private static T DeserializeRequired<T>(string path, JsonSerializerOptions options)
    {
        if (!File.Exists(path))
            throw new JsonDataStoreException($"Required data file not found: {path}");

        var json = File.ReadAllText(path);
        var value = JsonSerializer.Deserialize<T>(json, options);
        if (value is null)
            throw new JsonDataStoreException($"Failed to deserialize data file: {path}");

        return value;
    }

    private static void Validate(ApplicationsDataFile apps, PayerRulesDataFile rules, ReadinessConfigFile cfg)
    {
        if (apps.Applications is null || apps.Applications.Count == 0)
            throw new JsonDataStoreException("applications.json contains no applications.");

        var duplicateApps = apps.Applications
            .GroupBy(a => a.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicateApps.Length > 0)
            throw new JsonDataStoreException($"Duplicate application IDs found: {string.Join(", ", duplicateApps)}");

        var payerIds = rules.Payers.Select(p => p.PayerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (payerIds.Count == 0)
            throw new JsonDataStoreException("payer-rules.json contains no payers.");

        // Basic rules sanity: version intervals
        foreach (var payer in rules.Payers)
        {
            foreach (var v in payer.RuleVersions)
            {
                if (v.EffectiveTo is not null && v.EffectiveTo <= v.EffectiveFrom)
                    throw new JsonDataStoreException($"Invalid effective interval for payer {payer.PayerId} version {v.RuleVersionId}.");
            }
        }

        if (cfg.ExpirationWindowDays is < 1 or > 3650)
            throw new JsonDataStoreException("readiness-config.json ExpirationWindowDays must be between 1 and 3650.");
    }
}

public sealed class Snapshot
{
    public Snapshot(ApplicationsDataFile applications, PayerRulesDataFile payerRules, ReadinessConfigFile config)
    {
        Applications = applications;
        PayerRules = payerRules;
        Config = config;
    }

    public ApplicationsDataFile Applications { get; }
    public PayerRulesDataFile PayerRules { get; }
    public ReadinessConfigFile Config { get; }
}

public sealed class JsonDataStoreException : Exception
{
    public JsonDataStoreException(string message) : base(message) { }
    public JsonDataStoreException(string message, Exception inner) : base(message, inner) { }
}