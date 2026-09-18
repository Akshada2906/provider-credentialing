namespace ProviderEnrollment.Api.Services;

public sealed class NotFoundException : Exception
{
  public NotFoundException() : base("Resource not found.") { }
  public NotFoundException(string message) : base(message) { }
}

public sealed class BadRequestException : Exception
{
  public BadRequestException(string message) : base(message) { }
}
