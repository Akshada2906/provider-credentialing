using ProviderEnrollment.Api.Contracts;
using ProviderEnrollment.Api.Models;
using ProviderEnrollment.Api.Services;
using Xunit;

namespace ProviderEnrollment.Api.Tests;

public sealed class ReadinessEvaluationServiceTests
{
  [Fact]
  public void Expiration_Boundaries_AreCorrect_ForThreshold90()
  {
    var cfg = new ReadinessConfig { ExpirationThresholdDays = 90, FixedEvaluationDate = new DateOnly(2026, 9, 18) };

    var rules = new List<PayerRuleVersion>
    {
      new()
      {
        PayerId = "PAYER-003",
        PayerName = "Demo Payer 003",
        ApplicationType = "NEW_ENROLLMENT",
        RuleVersionId = "V1",
        EffectiveFrom = new DateOnly(2026, 1, 1),
        EffectiveTo = null,
        RequiredDocuments = new()
        {
          new RequiredDocumentRule { Key = "DOC_LICENSE", Label = "State License", RequiresExpiration = true }
        },
        RequiredFields = new()
      }
    };

    var appMinus1 = MakeApp("A-1", "2026-09-17"); // -1 day => expired (expiresOn < evalDate)
    var app0 = MakeApp("A0", "2026-09-18"); // 0 => expiring soon
    var app90 = MakeApp("A90", "2026-12-17"); // 90 => expiring soon
    var app91 = MakeApp("A91", "2026-12-18"); // 91 => present & valid

    var data = new InMemoryDataService(cfg, new List<EnrollmentApplication> { appMinus1, app0, app90, app91 }, rules);
    var svc = new ReadinessEvaluationService(data);

    Assert.Equal(ReadinessStatuses.Requirement.Expired, svc.Evaluate(appMinus1).Payers[0].Requirements[0].Status);
    Assert.Equal(ReadinessStatuses.Requirement.ExpiringSoon, svc.Evaluate(app0).Payers[0].Requirements[0].Status);
    Assert.Equal(ReadinessStatuses.Requirement.ExpiringSoon, svc.Evaluate(app90).Payers[0].Requirements[0].Status);
    Assert.Equal(ReadinessStatuses.Requirement.PresentAndValid, svc.Evaluate(app91).Payers[0].Requirements[0].Status);
  }

  [Fact]
  public void OverallStatus_Precedence_IsIncompleteThenExpiringThenReady()
  {
    var cfg = new ReadinessConfig { ExpirationThresholdDays = 90, FixedEvaluationDate = new DateOnly(2026, 9, 18) };

    var rules = new List<PayerRuleVersion>
    {
      new()
      {
        PayerId = "PAYER-A",
        PayerName = "A",
        ApplicationType = "NEW_ENROLLMENT",
        RuleVersionId = "VA",
        EffectiveFrom = new DateOnly(2026, 1, 1),
        RequiredDocuments = new(){ new RequiredDocumentRule { Key = "DOC_A", Label = "A", RequiresExpiration = false } },
        RequiredFields = new()
      },
      new()
      {
        PayerId = "PAYER-B",
        PayerName = "B",
        ApplicationType = "NEW_ENROLLMENT",
        RuleVersionId = "VB",
        EffectiveFrom = new DateOnly(2026, 1, 1),
        RequiredDocuments = new(){ new RequiredDocumentRule { Key = "DOC_B", Label = "B", RequiresExpiration = false } },
        RequiredFields = new()
      },
      new()
      {
        PayerId = "PAYER-C",
        PayerName = "C",
        ApplicationType = "NEW_ENROLLMENT",
        RuleVersionId = "VC",
        EffectiveFrom = new DateOnly(2026, 1, 1),
        RequiredDocuments = new(){ new RequiredDocumentRule { Key = "DOC_C", Label = "C", RequiresExpiration = true } },
        RequiredFields = new()
      }
    };

    var app = new EnrollmentApplication
    {
      Id = "APP",
      ProviderId = "P",
      ProviderName = "Provider",
      ApplicationType = "NEW_ENROLLMENT",
      IsActive = true,
      SubmittedOn = null,
      LastUpdated = DateTime.UtcNow,
      TargetPayers = new() { new TargetPayer { PayerId = "PAYER-A" }, new TargetPayer { PayerId = "PAYER-B" }, new TargetPayer { PayerId = "PAYER-C" } },
      Documents = new()
      {
        new ApplicationDocument{ DocumentId="1", Key="DOC_A", Label="A" },
        // Missing DOC_B => incomplete for B
        new ApplicationDocument{ DocumentId="3", Key="DOC_C", Label="C", ExpiresOn = new DateOnly(2026, 11, 2) } // 45 days => expiring soon
      },
      Fields = new()
    };

    var data = new InMemoryDataService(cfg, new List<EnrollmentApplication> { app }, rules);
    var svc = new ReadinessEvaluationService(data);

    var eval = svc.Evaluate(app);
    Assert.Equal(ReadinessStatuses.Payer.Incomplete, eval.OverallStatus);
    Assert.Equal(ReadinessStatuses.Payer.ReadyToSubmit, eval.Payers.Single(p => p.PayerId == "PAYER-A").Status);
    Assert.Equal(ReadinessStatuses.Payer.Incomplete, eval.Payers.Single(p => p.PayerId == "PAYER-B").Status);
    Assert.Equal(ReadinessStatuses.Payer.ExpiringSoon, eval.Payers.Single(p => p.PayerId == "PAYER-C").Status);
  }

  [Fact]
  public void MissingField_IsBlockingIncomplete()
  {
    var cfg = new ReadinessConfig { ExpirationThresholdDays = 90, FixedEvaluationDate = new DateOnly(2026, 9, 18) };

    var rules = new List<PayerRuleVersion>
    {
      new()
      {
        PayerId = "PAYER-2",
        PayerName = "P2",
        ApplicationType = "NEW_ENROLLMENT",
        RuleVersionId = "V1",
        EffectiveFrom = new DateOnly(2026, 1, 1),
        RequiredDocuments = new(){ new RequiredDocumentRule { Key = "DOC_CV", Label = "CV", RequiresExpiration = false } },
        RequiredFields = new(){ new RequiredFieldRule { Key = "TAX_ID", Label = "Tax ID" } }
      }
    };

    var app = new EnrollmentApplication
    {
      Id = "APP",
      ProviderId = "P",
      ProviderName = "Provider",
      ApplicationType = "NEW_ENROLLMENT",
      IsActive = true,
      SubmittedOn = null,
      LastUpdated = DateTime.UtcNow,
      TargetPayers = new() { new TargetPayer { PayerId = "PAYER-2" } },
      Documents = new() { new ApplicationDocument { DocumentId = "1", Key = "DOC_CV", Label = "CV" } },
      Fields = new() { ["TAX_ID"] = "" }
    };

    var data = new InMemoryDataService(cfg, new List<EnrollmentApplication> { app }, rules);
    var svc = new ReadinessEvaluationService(data);

    var eval = svc.Evaluate(app);
    Assert.Equal(ReadinessStatuses.Payer.Incomplete, eval.OverallStatus);
    Assert.Contains(eval.Payers[0].Requirements, r => r.Kind == ReadinessStatuses.RequirementKind.Field && r.Status == ReadinessStatuses.Requirement.Missing);
  }

  [Fact]
  public void HistoricalSubmittedOn_SelectsOlderRuleVersion()
  {
    var cfg = new ReadinessConfig { ExpirationThresholdDays = 90, FixedEvaluationDate = new DateOnly(2026, 9, 18) };

    var rules = new List<PayerRuleVersion>
    {
      new()
      {
        PayerId = "PAYER-1",
        PayerName = "P1",
        ApplicationType = "NEW_ENROLLMENT",
        RuleVersionId = "OLD",
        EffectiveFrom = new DateOnly(2026, 1, 1),
        EffectiveTo = new DateOnly(2026, 9, 1),
        RequiredDocuments = new(){ new RequiredDocumentRule { Key = "DOC_W9", Label = "W9", RequiresExpiration = false } },
        RequiredFields = new()
      },
      new()
      {
        PayerId = "PAYER-1",
        PayerName = "P1",
        ApplicationType = "NEW_ENROLLMENT",
        RuleVersionId = "NEW",
        EffectiveFrom = new DateOnly(2026, 9, 1),
        EffectiveTo = null,
        RequiredDocuments = new(){ new RequiredDocumentRule { Key = "DOC_W9", Label = "W9", RequiresExpiration = false } },
        RequiredFields = new()
      }
    };

    var app = new EnrollmentApplication
    {
      Id = "APP",
      ProviderId = "P",
      ProviderName = "Provider",
      ApplicationType = "NEW_ENROLLMENT",
      IsActive = true,
      SubmittedOn = new DateOnly(2026, 8, 15),
      LastUpdated = DateTime.UtcNow,
      TargetPayers = new() { new TargetPayer { PayerId = "PAYER-1" } },
      Documents = new() { new ApplicationDocument { DocumentId = "1", Key = "DOC_W9", Label = "W9" } },
      Fields = new()
    };

    var data = new InMemoryDataService(cfg, new List<EnrollmentApplication> { app }, rules);
    var svc = new ReadinessEvaluationService(data);

    var eval = svc.Evaluate(app);
    Assert.Equal(ReadinessStatuses.EvaluationDateSource.SubmittedOn, eval.EvaluationDateSource);
    Assert.Equal(new DateOnly(2026, 8, 15), eval.EvaluationDate);
    Assert.Equal("OLD", eval.Payers[0].RuleVersionId);
  }

  private static EnrollmentApplication MakeApp(string id, string expiresOn)
  {
    return new EnrollmentApplication
    {
      Id = id,
      ProviderId = "P",
      ProviderName = "Provider",
      ApplicationType = "NEW_ENROLLMENT",
      IsActive = true,
      SubmittedOn = null,
      LastUpdated = DateTime.UtcNow,
      TargetPayers = new() { new TargetPayer { PayerId = "PAYER-003" } },
      Documents = new()
      {
        new ApplicationDocument { DocumentId = "D", Key = "DOC_LICENSE", Label = "State License", ExpiresOn = DateOnly.Parse(expiresOn) }
      },
      Fields = new()
    };
  }

  private sealed class InMemoryDataService : IEnrollmentDataService
  {
    public InMemoryDataService(ReadinessConfig cfg, IReadOnlyList<EnrollmentApplication> apps, IReadOnlyList<PayerRuleVersion> rules)
    {
      Config = cfg;
      ConfiguredEvaluationDate = cfg.FixedEvaluationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
      Applications = apps;
      RuleVersions = rules;
    }

    public IReadOnlyList<EnrollmentApplication> Applications { get; }
    public IReadOnlyList<PayerRuleVersion> RuleVersions { get; }
    public ReadinessConfig Config { get; }
    public DateOnly ConfiguredEvaluationDate { get; }

    public EnrollmentApplication? FindApplication(string id) => Applications.FirstOrDefault(a => a.Id == id);
  }
}