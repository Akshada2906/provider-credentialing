using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ProviderEnrollment.Api.Contracts;
using Xunit;

namespace ProviderEnrollment.Api.Tests;

public sealed class ApplicationsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
  private readonly WebApplicationFactory<Program> _factory;

  public ApplicationsControllerTests(WebApplicationFactory<Program> factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task GetApplications_ReturnsOnlyActive_AndHasCounts()
  {
    using var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/v1/applications?sortBy=providerName&sortDirection=asc");
    response.EnsureSuccessStatusCode();

    var dto = await response.Content.ReadFromJsonAsync<ApplicationListResponseDto>();
    Assert.NotNull(dto);

    // ensure inactive excluded from list items
    Assert.DoesNotContain(dto!.Items, i => i.Id == "APP-INACTIVE-001");

    // counts present
    Assert.True(dto.StatusCounts.ReadyToSubmit >= 0);
    Assert.True(dto.StatusCounts.Incomplete >= 0);
    Assert.True(dto.StatusCounts.ExpiringSoon >= 0);
    Assert.Equal(new DateOnly(2026, 9, 18), dto.ConfiguredEvaluationDate);
  }

  [Fact]
  public async Task GetApplications_InvalidQuery_Returns400()
  {
    using var client = _factory.CreateClient();
    var response = await client.GetAsync("/api/v1/applications?sortBy=badField");
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var err = await response.Content.ReadFromJsonAsync<ApiErrorDto>();
    Assert.NotNull(err);
    Assert.Equal("INVALID_QUERY", err!.Code);
  }

  [Fact]
  public async Task GetApplicationById_Unknown_Returns404()
  {
    using var client = _factory.CreateClient();
    var response = await client.GetAsync("/api/v1/applications/DOES-NOT-EXIST");
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }
}