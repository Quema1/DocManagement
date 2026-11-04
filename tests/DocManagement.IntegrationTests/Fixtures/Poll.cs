namespace DocManagement.IntegrationTests.Fixtures;

internal static class Poll
{
    public static async Task<T> UntilAsync<T>(Func<Task<T>> probe, Func<T, bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TestConstants.PollTimeout;

        while (true)
        {
            var value = await probe();
            if (condition(value) || DateTime.UtcNow >= deadline)
            {
                return value;
            }

            await Task.Delay(TestConstants.PollInterval, cancellationToken);
        }
    }
}
