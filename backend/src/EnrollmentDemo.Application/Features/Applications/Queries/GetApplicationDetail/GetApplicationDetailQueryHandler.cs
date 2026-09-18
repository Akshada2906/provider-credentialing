using EnrollmentDemo.Application.Common.Exceptions;
using EnrollmentDemo.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace EnrollmentDemo.Application.Features.Applications.Queries.GetApplicationDetail;

public sealed class GetApplicationDetailQueryHandler : IRequestHandler<GetApplicationDetailQuery, ApplicationEvaluationDto>
{
    private readonly IReadinessEvaluationService _readinessEvaluationService;
    private readonly IValidator<GetApplicationDetailQuery> _validator;

    public GetApplicationDetailQueryHandler(
        IReadinessEvaluationService readinessEvaluationService,
        IValidator<GetApplicationDetailQuery> validator)
    {
        _readinessEvaluationService = readinessEvaluationService;
        _validator = validator;
    }

    public async Task<ApplicationEvaluationDto> Handle(GetApplicationDetailQuery request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var result = await _readinessEvaluationService.EvaluateApplicationAsync(request.Id, cancellationToken);
        if (result is null)
        {
            throw new NotFoundException("Application not found");
        }

        return result;
    }
}