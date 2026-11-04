using DocManagement.Domain;
using DocManagement.Domain.Constants;

namespace DocManagement.Infrastructure.Messaging.Outbox;

public static class OutboxRetryPolicy
{
    public static bool RegisterFailure(OutboxMessage message, string error, DateTime now)
    {
        message.Attempts++;
        message.Error = error.Length > OutboxConstants.ErrorMaxLength
            ? error[..OutboxConstants.ErrorMaxLength]
            : error;

        if (message.Attempts >= OutboxConstants.MaxAttempts)
        {
            message.ProcessedAt = now;
            message.NextAttemptAt = null;
            return true;
        }

        message.NextAttemptAt = now + GetRetryDelay(message.Attempts);
        return false;
    }

    public static TimeSpan GetRetryDelay(int attempts) =>
        TimeSpan.FromMinutes(Math.Min(Math.Pow(2, attempts), OutboxConstants.MaxRetryDelayMinutes));
}
