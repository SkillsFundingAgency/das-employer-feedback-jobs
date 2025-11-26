using Microsoft.Extensions.Logging;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public abstract class BaseFunction<T>
    {
        protected readonly ILogger<T> Logger;
        protected const int MaxRetryAttempts = 3;

        protected BaseFunction(ILogger<T> logger)
        {
            Logger = logger;
        }

        protected async Task<TResult> ExecuteWithRetry<TResult>(Func<Task<TResult>> func, int maxAttempts, CancellationToken cancellationToken)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    return await func();
                }
                catch (Exception ex) when (attempt < maxAttempts - 1)
                {
                    attempt++;
                    Logger.LogWarning(ex, "Retrying operation (attempt {CurrentAttempt} of {MaxAttempts})",
                        attempt + 1, maxAttempts);

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
    }
}