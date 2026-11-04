namespace DocManagement.Domain.Constants;

public static class MessagingConstants
{
    public const string PreviewQueueName = "document-previews";
    public const string ClientName = "DocManagement.Api";
    public const ushort PrefetchCount = 10;
    public const int ReconnectDelaySeconds = 5;
}
