using DocManagement.Domain;
using DocManagement.Domain.Constants;
using DocManagement.Infrastructure.Messaging.Outbox;

namespace DocManagement.UnitTests.Messaging;

public class OutboxRetryPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(5, 32)]
    [InlineData(6, OutboxConstants.MaxRetryDelayMinutes)]
    [InlineData(9, OutboxConstants.MaxRetryDelayMinutes)]
    public void GetRetryDelay_GrowsExponentiallyUpToCap(int attempts, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), OutboxRetryPolicy.GetRetryDelay(attempts));
    }

    [Fact]
    public void RegisterFailure_SchedulesRetry_WhenAttemptsRemain()
    {
        var message = CreateMessage();

        var deadLettered = OutboxRetryPolicy.RegisterFailure(message, "broker rejected", Now);

        Assert.False(deadLettered);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("broker rejected", message.Error);
        Assert.Equal(Now + OutboxRetryPolicy.GetRetryDelay(1), message.NextAttemptAt);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public void RegisterFailure_DeadLetters_OnLastAttempt()
    {
        var message = CreateMessage(attempts: OutboxConstants.MaxAttempts - 1);
        message.NextAttemptAt = Now;

        var deadLettered = OutboxRetryPolicy.RegisterFailure(message, "broker rejected", Now);

        Assert.True(deadLettered);
        Assert.Equal(OutboxConstants.MaxAttempts, message.Attempts);
        Assert.Equal(Now, message.ProcessedAt);
        Assert.Null(message.NextAttemptAt);
    }

    [Fact]
    public void RegisterFailure_TruncatesLongError()
    {
        var message = CreateMessage();

        OutboxRetryPolicy.RegisterFailure(message, new string('x', OutboxConstants.ErrorMaxLength + 10), Now);

        Assert.Equal(OutboxConstants.ErrorMaxLength, message.Error!.Length);
    }

    private static OutboxMessage CreateMessage(int attempts = 0) => new()
    {
        Id = Guid.NewGuid(),
        Type = OutboxConstants.PreviewRequestedType,
        Payload = Guid.NewGuid().ToString(),
        OccurredAt = Now,
        Attempts = attempts,
    };
}
