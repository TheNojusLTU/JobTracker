namespace JobTracker.Contracts;

public sealed record CsrfResponse(string RequestToken, string HeaderName);
