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

builder.Services.Configure<JsonDataOptions>(builder.Configuration.GetSection("Data"));
builder.Services.AddSingleton<IEnrollmentDataService, JsonEnrollmentDataService>();
builder.Services.AddSingleton<IReadinessEvaluationService, ReadinessEvaluationService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
  options.AddPolicy("DevCors", policy =>
  {
    var origin = builder.Configuration["Cors:FrontendOrigin"];
    if (!string.IsNullOrWhiteSpace(origin))
    {
      policy.WithOrigins(origin)
        .AllowAnyHeader()
        .AllowAnyMethod();
    }
    else
    {
      policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }
  });
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
  options.InvalidModelStateResponseFactory = context =>
  {
    var problem = new ProblemDetails
    {
      Title = "Request validation failed.",
      Status = StatusCodes.Status400BadRequest,
      Type = "https://httpstatuses.com/400"
    };

    problem.Extensions["code"] = "INVALID_REQUEST";
    problem.Extensions["errors"] = context.ModelState
      .Where(kvp => kvp.Value?.Errors.Count > 0)
      .ToDictionary(
        kvp => kvp.Key,
        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

    return new BadRequestObjectResult(problem);
  };
});

var app = builder.Build();

app.UseExceptionHandler(handlerApp =>
{
  handlerApp.Run(async context =>
  {
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
    var ex = feature?.Error;

    var (status, code, title) = ex switch
    {
      ProviderEnrollment.Api.Services.ConfigurationValidationException => (StatusCodes.Status500InternalServerError, "CONFIG_INVALID", "Server configuration is invalid."),
      ProviderEnrollment.Api.Services.NotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND", "Resource not found."),
      ProviderEnrollment.Api.Services.BadRequestException => (StatusCodes.Status400BadRequest, "BAD_REQUEST", "Request is invalid."),
      _ => (StatusCodes.Status500InternalServerError, "SERVER_ERROR", "An unexpected error occurred.")
    };

    var problem = new ProblemDetails
    {
      Title = title,
      Status = status,
      Type = $"https://httpstatuses.com/{status}"
    };

    problem.Extensions["code"] = code;

    context.Response.StatusCode = status;
    context.Response.ContentType = "application/problem+json";
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

using (var scope = app.Services.CreateScope())
{
  // Force startup validation and deterministic snapshot loading
  _ = scope.ServiceProvider.GetRequiredService<IEnrollmentDataService>();
}

app.Run();