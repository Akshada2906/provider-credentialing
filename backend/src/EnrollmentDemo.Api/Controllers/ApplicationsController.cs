using EnrollmentDemo.Application.Features.Applications.Queries.GetApplicationDetail;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnrollmentDemo.Api.Controllers;

[ApiController]
[Route("api/v1/applications")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApplicationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApplicationEvaluationDto>> GetById([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetApplicationDetailQuery(id), cancellationToken);
        return Ok(result);
    }
}