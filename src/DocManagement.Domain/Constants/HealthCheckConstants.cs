namespace DocManagement.Domain.Constants;

public static class HealthCheckConstants
{
    public const string Path = "/health";
    public const string DatabaseCheckName = "sql-server";
    public const string RabbitMqCheckName = "rabbitmq";
    public const int TimeoutSeconds = 5;
}
