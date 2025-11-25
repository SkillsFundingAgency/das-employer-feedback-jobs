using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class GenerateFeedbackTransactionsFunction
    {
        private readonly ILogger<GenerateFeedbackTransactionsFunction> _logger;
        private readonly IEmployerFeedbackOuterApi _api;
        private readonly ApplicationConfiguration _configuration;
        private const int MaxRetryAttempts = 3;

        public GenerateFeedbackTransactionsFunction(
            ILogger<GenerateFeedbackTransactionsFunction> log,
            IEmployerFeedbackOuterApi api,
            ApplicationConfiguration configuration)
        {
            _logger = log;
            _api = api;
            _configuration = configuration;
        }

        [Function(nameof(GenerateFeedbackTransactionsTimer))]
        public async Task GenerateFeedbackTransactionsTimer([TimerTrigger("%GenerateFeedbackTransactionsTimerSchedule%", RunOnStartup = true)] TimerInfo timer)
        {
            try
            {
                _logger.LogInformation("GenerateFeedbackTransactionsFunction has started");

                var accountIdsResponse = await ExecuteWithRetry(async () =>
                {
                    _logger.LogDebug("Fetching feedback transaction account IDs");
                    return await _api.GetFeedbackTransactionAccountIds(_configuration.GenerateFeedbackTransactionsBatchSize);
                }, MaxRetryAttempts, CancellationToken.None);

                var accountIds = accountIdsResponse.AccountIds;

                _logger.LogInformation("Retrieved {AccountCount} feedback transaction accounts for processing", accountIds.Count);

                await ProcessAccountsInParallelAsync(accountIds, _configuration.GenerateFeedbackTransactionsMaxParallelism);

                _logger.LogInformation("GenerateFeedbackTransactionsFunction has finished successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateFeedbackTransactionsFunction has failed");
                throw;
            }
        }

        private async Task ProcessAccountsInParallelAsync(IEnumerable<string> accountIds, int maxParallelism)
        {
            _logger.LogInformation("Processing {AccountCount} accounts with max parallelism of {MaxParallelism}",
                accountIds.Count(), maxParallelism);

            var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
            var processedCount = 0;
            var failedCount = 0;

            var tasks = accountIds.Select(async accountId =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await ProcessSingleAccountAsync(accountId);
                    Interlocked.Increment(ref processedCount);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failedCount);
                    _logger.LogError(ex, "Failed to process feedback transaction for account {AccountId} after {MaxRetryAttempts} attempts",
                        accountId, MaxRetryAttempts);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Processing completed. Processed: {ProcessedCount}, Failed: {FailedCount}",
                processedCount, failedCount);
        }

        private async Task ProcessSingleAccountAsync(string accountId)
        {
            await ExecuteWithRetry(async () =>
            {
                await _api.ProcessFeedbackTransactionForAccount(accountId);
                _logger.LogDebug("Successfully processed feedback transaction for account {AccountId}", accountId);
                return true;
            }, MaxRetryAttempts, CancellationToken.None);
        }

        private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, int maxAttempts, CancellationToken cancellationToken)
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
                    _logger.LogWarning(ex, "Retrying operation (attempt {CurrentAttempt} of {MaxAttempts})",
                        attempt + 1, maxAttempts);

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));

                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
    }
}