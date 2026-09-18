using FluentValidation;

namespace EnrollmentDemo.Application.Features.Applications.Queries.GetApplicationDetail;

public sealed class GetApplicationDetailQueryValidator : AbstractValidator<GetApplicationDetailQuery>
{
    public GetApplicationDetailQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .Must(BeValidId)
            .WithMessage("Invalid application ID format");
    }

    private static bool BeValidId(string id)
    {
        // POC rule: allow common synthetic IDs like "app-001" and GUIDs. Reject whitespace/unsafe chars.
        if (id.Length > 64) return false;
        for (var i = 0; i < id.Length; i++)
        {
            var c = id[i];
            var ok = char.IsLetterOrDigit(c) || c is '-' or '_' ;
            if (!ok) return false;
        }
        return true;
    }
}