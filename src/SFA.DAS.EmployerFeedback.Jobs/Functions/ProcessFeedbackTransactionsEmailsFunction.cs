using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class ProcessFeedbackTransactionsEmailsFunction
    {
        private readonly ILogger<ProcessFeedbackTransactionsEmailsFunction> _logger;
        private readonly IEmployerFeedbackOuterApi _api;
        private readonly ApplicationConfiguration _configuration;
        private const int MaxRetryAttempts = 3;

        public ProcessFeedbackTransactionsEmailsFunction(
            ILogger<ProcessFeedbackTransactionsEmailsFunction> logger,
            IEmployerFeedbackOuterApi api,
            ApplicationConfiguration configuration)
        {
            _logger = logger;
            _api = api;
            _configuration = configuration;
        }

        [Function(nameof(ProcessFeedbackTransactionsEmailsTimer))]
        public async Task ProcessFeedbackTransactionsEmailsTimer([TimerTrigger("%ProcessFeedbackTransactionsEmailsSchedule%", RunOnStartup = false)] TimerInfo timer)
        {
            try
            {
                _logger.LogInformation("ProcessFeedbackTransactionsEmails started");

                var response = await ExecuteWithRetry(async () =>
                {
                    _logger.LogDebug("Fetching feedback transactions to email");
                    return await _api.GetFeedbackTransactionsBatch(_configuration.ProcessFeedbackEmailsBatchSize);
                }, MaxRetryAttempts, CancellationToken.None);

                var transactionIds = response.FeedbackTransactions;

                _logger.LogInformation("Processing {Count} feedback transactions in batches of {BatchSize}",
                    transactionIds.Count, _configuration.ProcessFeedbackEmailsMaxParallelism);

                var totalProcessed = 0;
                var totalFailed = 0;

                for (int i = 0; i < transactionIds.Count; i += _configuration.ProcessFeedbackEmailsMaxParallelism)
                {
                    var batch = transactionIds.Skip(i).Take(_configuration.ProcessFeedbackEmailsMaxParallelism).ToList();
                    var batchNumber = (i / _configuration.ProcessFeedbackEmailsMaxParallelism) + 1;

                    _logger.LogInformation("Processing batch {BatchNumber} ({Count} transactions)",
                        batchNumber, batch.Count);

                    var result = await ProcessBatch(batch, batchNumber);
                    totalProcessed += result.processed;
                    totalFailed += result.failed;

                    _logger.LogInformation("Batch {BatchNumber} completed: {Processed} processed, {Failed} failed",
                        batchNumber, result.processed, result.failed);

                    if (i + _configuration.ProcessFeedbackEmailsMaxParallelism < transactionIds.Count)
                    {
                        await Task.Delay(1000);
                    }
                }

                _logger.LogInformation("ProcessFeedbackTransactionsEmails completed: {TotalProcessed} processed, {TotalFailed} failed",
                    totalProcessed, totalFailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessFeedbackTransactionsEmails failed");
                throw;
            }
        }

        private async Task<(int processed, int failed)> ProcessBatch(List<long> transactionIds, int batchNumber)
        {
            var processed = 0;
            var failed = 0;

            var tasks = transactionIds.Select(async transactionId =>
            {
                try
                {
                    await ProcessSingleEmailAsync(transactionId);
                    Interlocked.Increment(ref processed);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    _logger.LogError(ex, "Failed to send email for transaction {TransactionId} in batch {BatchNumber} after {MaxRetryAttempts} attempts",
                        transactionId, batchNumber, MaxRetryAttempts);
                }
            });

            await Task.WhenAll(tasks);
            return (processed, failed);
        }

        private async Task ProcessSingleEmailAsync(long transactionId)
        {
            await ExecuteWithRetry(async () =>
            {
                var request = new SendFeedbackEmailsRequest
                {
                    NotificationTemplates = _configuration.NotificationTemplates,
                    EmployerAccountsBaseUrl = _configuration.EmployerAccountsBaseUrl,
                    EmployerFeedbackBaseUrl = _configuration.EmployerFeedbackBaseUrl
                };

                _logger.LogDebug("Sending email for transaction {TransactionId} with templates: {@Templates} and base URL: {BaseUrl}",
                    transactionId, request.NotificationTemplates, request.EmployerAccountsBaseUrl);

                await _api.SendFeedbackEmails(transactionId, request);

                _logger.LogDebug("Successfully sent email for transaction {TransactionId}", transactionId);

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