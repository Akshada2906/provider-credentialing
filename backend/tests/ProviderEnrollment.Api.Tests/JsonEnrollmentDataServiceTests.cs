using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProviderEnrollment.Api.Services;
using Xunit;

namespace ProviderEnrollment.Api.Tests;

public sealed class JsonEnrollmentDataServiceTests
{
  [Fact]
  public void Throws_OnMissingFiles()
  {
    var cfg = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Data:ApplicationsPath"] = "Data/does-not-exist-apps.json",
        ["Data:PayerRulesPath"] = "Data/does-not-exist-rules.json",
        ["Data:ReadinessConfigPath"] = "Data/does-not-exist-config.json",
      })
      .Build();

    var env = new FakeEnv(Path.GetTempPath());

    Assert.Throws<InvalidOperationException>(() => new JsonEnrollmentDataService(cfg, env));
  }

  [Fact]
  public void Throws_OnInvalidThreshold()
  {
    var root = CreateTempDataFolder();
    File.WriteAllText(Path.Combine(root, "readiness-config.json"), """{ "expirationThresholdDays": 0, "fixedEvaluationDate": "2026-09-18" }""");
    File.WriteAllText(Path.Combine(root, "applications.json"), "[]");
    File.WriteAllText(Path.Combine(root, "payer-rules.json"), "[]");

    var cfg = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Data:ApplicationsPath"] = "applications.json",
        ["Data:PayerRulesPath"] = "payer-rules.json",
        ["Data:ReadinessConfigPath"] = "readiness-config.json",
      })
      .Build();

    var env = new FakeEnv(root);

    Assert.Throws<InvalidOperationException>(() => new JsonEnrollmentDataService(cfg, env));
  }

  private static string CreateTempDataFolder()
  {
    var dir = Path.Combine(Path.GetTempPath(), "ProviderEnrollmentTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  private sealed class FakeEnv : IWebHostEnvironment
  {
    public FakeEnv(string contentRootPath)
    {
      ContentRootPath = contentRootPath;
      EnvironmentName = Environments.Development;
      ApplicationName = "ProviderEnrollment.Api.Tests";
      WebRootPath = contentRootPath;
      WebRootFileProvider = null!;
      ContentRootFileProvider = null!;
    }

    public string ApplicationName { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public string EnvironmentName { get; set; }
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
  }
}