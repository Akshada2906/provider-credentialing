namespace ProviderEnrollment.Api.Contracts;

public static class ReadinessStatuses
{
  public static class Payer
  {
    public const string ReadyToSubmit = "Ready to Submit";
    public const string Incomplete = "Incomplete";
    public const string ExpiringSoon = "Expiring Soon";
  }

  public static class Requirement
  {
    public const string PresentAndValid = "Present & Valid";
    public const string Missing = "Missing";
    public const string Expired = "Expired";
    public const string ExpiringSoon = "Expiring Soon";
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
    public const string UtcNow = "UTC_NOW";
  }
}