namespace ProviderEnrollment.Api.Contracts;

public static class ReadinessStatuses
{
  public static class Payer
  {
    public const string ReadyToSubmit = "Ready to Submit";
    public const string Incomplete = "Incomplete";
    public const string ExpiringSoon = "Expiring Soon";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
      ReadyToSubmit,
      Incomplete,
      ExpiringSoon
    };
  }

  public static class Requirement
  {
    public const string PresentAndValid = "Present & Valid";
    public const string Missing = "Missing";
    public const string Expired = "Expired";
    public const string ExpiringSoon = "Expiring Soon";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
      PresentAndValid,
      Missing,
      Expired,
      ExpiringSoon
    };
  }

  public static class RequirementKind
  {
    public const string Document = "DOCUMENT";
    public const string Field = "FIELD";
  }

  public static class EvaluationDateSource
  {
    public const string SubmittedOn = "SUBMITTED_ON";
    public const string Configured = "CONFIGURED";
    public const string Current = "CURRENT";
  }

  public static string? CombineOverall(params string?[] payerStatuses)
  {
    // Overall application precedence: Incomplete > Expiring Soon > Ready to Submit
    if (payerStatuses.Any(s => s == null))
      return null;

    if (payerStatuses.Any(s => s == Payer.Incomplete))
      return Payer.Incomplete;

    if (payerStatuses.Any(s => s == Payer.ExpiringSoon))
      return Payer.ExpiringSoon;

    return Payer.ReadyToSubmit;
  }
}