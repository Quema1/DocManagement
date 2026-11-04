namespace DocManagement.IntegrationTests.Fixtures;

internal static class TestConstants
{
    public const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";
    public const string RabbitMqImage = "rabbitmq:4-management";
    public const string DatabaseName = "DocManagementTests";
    public const string PreviewBaseUrl = "https://previews.test";
    public const string PreviewUrlPattern = "^https://previews\\.test/previews/[0-9a-f]{16}\\.png$";

    public static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
}
