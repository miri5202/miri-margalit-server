namespace WorkshopHub.Core.Common;

/// <summary>JWT חתום ומועד התפוגה שלו.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
