using System.Net.Mime;
using EnrollmentDemo.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace EnrollmentDemo.Api.Middleware;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            var problem = CreateProblemDetails(context, StatusCodes.Status400BadRequest, "Validation failed.");
            problem.Extensions["errors"] = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            await WriteProblemDetails(context, problem);
        }
        catch (NotFoundException ex)
        {
            var problem = CreateProblemDetails(context, StatusCodes.Status404NotFound, ex.Message);
            await WriteProblemDetails(context, problem);
        }
        catch (JsonDataStoreException ex)
        {
            var problem = CreateProblemDetails(context, StatusCodes.Status500InternalServerError, ex.Message);
            await WriteProblemDetails(context, problem);
        }
        catch (Exception)
        {
            var problem = CreateProblemDetails(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            await WriteProblemDetails(context, problem);
        }
    }

    private static ProblemDetails CreateProblemDetails(HttpContext context, int statusCode, string detail)
    {
        return new ProblemDetails
        {
            Type = "about:blank",
            Title = statusCode switch
            {
                StatusCodes.Status400BadRequest => "Bad Request",
                StatusCodes.Status404NotFound => "Not Found",
                _ => "Server Error"
            },
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };
    }

    private static async Task WriteProblemDetails(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = MediaTypeNames.Application.Json;
        await context.Response.WriteAsJsonAsync(problem);
    }
}