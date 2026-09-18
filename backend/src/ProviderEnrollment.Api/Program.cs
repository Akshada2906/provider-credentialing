using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ProviderEnrollment.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
  .AddJsonOptions(o =>
  {
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
  });

builder.Services.Configure<ApiBehaviorOptions>(o =>
{
  // Ensure consistent ProblemDetails for model-binding errors
  o.SuppressModelStateInvalidFilter = false;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IEnrollmentDataService, JsonEnrollmentDataService>();
builder.Services.AddSingleton<IReadinessEvaluationService, ReadinessEvaluationService>();

builder.Services.AddCors(options =>
{
  options.AddPolicy("DevCors", p =>
  {
    var origin = builder.Configuration["Frontend:Origin"];
    if (!string.IsNullOrWhiteSpace(origin))
    {
      p.WithOrigins(origin)
        .AllowAnyHeader()
        .AllowAnyMethod();
    }
  });
});

var app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
  exceptionApp.Run(async context =>
  {
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";

    var problem = new ProblemDetails
    {
      Status = StatusCodes.Status500InternalServerError,
      Title = "An unexpected error occurred.",
      Type = "https://httpstatuses.com/500",
      Extensions =
      {
        ["code"] = "UNHANDLED_ERROR"
      }
    };

    await context.Response.WriteAsJsonAsync(problem);
  });
});

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
  app.UseCors("DevCors");
}

app.MapControllers();

// Force-load and validate data at startup (fail fast)
using (var scope = app.Services.CreateScope())
{
  _ = scope.ServiceProvider.GetRequiredService<IEnrollmentDataService>();
}

app.Run();

public partial class Program { }