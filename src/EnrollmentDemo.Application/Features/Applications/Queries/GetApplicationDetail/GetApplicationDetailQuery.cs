using MediatR;

namespace EnrollmentDemo.Application.Features.Applications.Queries.GetApplicationDetail;

public sealed record GetApplicationDetailQuery(string Id) : IRequest<ApplicationEvaluationDto>;