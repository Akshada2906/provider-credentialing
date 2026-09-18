using System.ComponentModel.DataAnnotations;

namespace EnrollmentDemo.Infrastructure.Options;

public sealed class ReadinessConfigOptions
{
    public const string SectionName = "ReadinessConfig";

    [Range(1, 3650)]
    public int ExpirationWindowDays { get; init; } = 90;

    public DateTimeOffset? ConfiguredEvaluationDateUtc { get; init; }
}