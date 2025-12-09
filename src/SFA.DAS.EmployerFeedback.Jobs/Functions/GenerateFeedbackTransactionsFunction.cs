using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class GenerateFeedbackTransactionsFunction : BaseFunction<GenerateFeedbackTransactionsFunction>
    {
        private readonly IEmployerFeedbackOuterApi _api;
        private readonly ApplicationConfiguration _configuration;

        public GenerateFeedbackTransactionsFunction(
            ILogger<GenerateFeedbackTransactionsFunction> logger,
            IEmployerFeedbackOuterApi api,
            ApplicationConfiguration configuration) : base(logger)
        {
            _api = api;
            _configuration = configuration;
        }

        [Function(nameof(GenerateFeedbackTransactionsTimer))]
        public async Task GenerateFeedbackTransactionsTimer([TimerTrigger("%GenerateFeedbackTransactionsTimerSchedule%", RunOnStartup = false)] TimerInfo timer)
        {
            try
            {
                Logger.LogInformation("GenerateFeedbackTransactionsFunction has started");

                var accountIdsResponse = await ExecuteWithRetry(async () =>
                {
                    Logger.LogInformation("Fetching feedback transaction account IDs");
                    return await _api.GetFeedbackTransactionAccountIds(_configuration.GenerateFeedbackTransactionsBatchSize);
                }, MaxRetryAttempts, CancellationToken.None);

                var accountIds = accountIdsResponse.AccountIds;

                Logger.LogInformation("Retrieved {AccountCount} feedback transaction accounts for processing", accountIds.Count);

                await ProcessAccountsInParallelAsync(accountIds, _configuration.GenerateFeedbackTransactionsMaxParallelism);

                Logger.LogInformation("GenerateFeedbackTransactionsFunction has finished successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GenerateFeedbackTransactionsFunction has failed");
                throw;
            }
        }

        private async Task ProcessAccountsInParallelAsync(IEnumerable<string> accountIds, int maxParallelism)
        {
            Logger.LogInformation("Processing {AccountCount} accounts with max parallelism of {MaxParallelism}",
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
                    Logger.LogError(ex, "Failed to process feedback transaction for account {AccountId} after {MaxRetryAttempts} attempts",
                        accountId, MaxRetryAttempts);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            Logger.LogInformation("Processing completed. Processed: {ProcessedCount}, Failed: {FailedCount}",
                processedCount, failedCount);
        }

        private async Task ProcessSingleAccountAsync(string accountId)
        {
            await ExecuteWithRetry(async () =>
            {
                Logger.LogInformation("Processing feedback transaction for account {AccountId}", accountId);
                await _api.ProcessFeedbackTransactionForAccount(accountId);
                Logger.LogInformation("Successfully processed feedback transaction for account {AccountId}", accountId);
                return true;
            }, MaxRetryAttempts, CancellationToken.None);
        }
    }
}