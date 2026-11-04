namespace DocManagement.Domain.Constants;

public static class OutboxConstants
{
    public const string PreviewRequestedType = "PreviewRequested";
    public const int TypeMaxLength = 100;
    public const int ErrorMaxLength = 2000;
    public const int BatchSize = 20;
    public const int MaxMessagesPerCycle = 1000;
    public const int MaxAttempts = 10;
    public const int MaxRetryDelayMinutes = 60;
    public const int PollIntervalSeconds = 2;
    public const int RetentionHours = 24;
    public const int CleanupIntervalMinutes = 60;
}
