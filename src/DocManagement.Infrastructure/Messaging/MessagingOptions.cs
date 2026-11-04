namespace DocManagement.Infrastructure.Messaging;

public class MessagingOptions
{
    public const string SectionName = "Messaging";

    public MessagingProvider Provider { get; set; } = MessagingProvider.Channel;
}
