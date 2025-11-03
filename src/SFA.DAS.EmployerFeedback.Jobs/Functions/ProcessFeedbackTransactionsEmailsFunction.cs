using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;
using SFA.DAS.EmployerFeedback.Jobs.Services;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class ProcessFeedbackTransactionsEmailsFunction
    {
        private readonly ILogger<ProcessFeedbackTransactionsEmailsFunction> _logger;
        private readonly IEmployerFeedbackOuterApi _api;
        private readonly ApplicationConfiguration _configuration;
        private readonly IWaveFanoutService _waveFanoutService;
        private const int MaxRetryAttempts = 3;

        public ProcessFeedbackTransactionsEmailsFunction(
            ILogger<ProcessFeedbackTransactionsEmailsFunction> logger,
            IEmployerFeedbackOuterApi api,
            ApplicationConfiguration configuration,
            IWaveFanoutService waveFanoutService)
        {
            _logger = logger;
            _api = api;
            _configuration = configuration;
            _waveFanoutService = waveFanoutService;
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

                _logger.LogInformation("Processing {Count} feedback transactions with wave fanout (max {MaxParallelism} per second)",
                    transactionIds.Count, _configuration.ProcessFeedbackEmailsMaxParallelism);

                var results = await _waveFanoutService.ExecuteAsync(
                    transactionIds,
                    ProcessSingleEmailAsync,
                    _configuration.ProcessFeedbackEmailsMaxParallelism,
                    1000);

                var successCount = results.Count(r => r);
                var failureCount = results.Count - successCount;

                _logger.LogInformation("ProcessFeedbackTransactionsEmails completed: {SuccessCount} successful, {FailureCount} failed",
                    successCount, failureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessFeedbackTransactionsEmails failed");
                throw;
            }
        }

        private async Task<bool> ProcessSingleEmailAsync(long transactionId)
        {
            try
            {
                _logger.LogDebug("Starting email processing for transaction {TransactionId}", transactionId);

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

                _logger.LogInformation("Successfully completed email processing for transaction {TransactionId}", transactionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email processing failed for transaction {TransactionId}: {ErrorMessage}",
                    transactionId, ex.Message);
                return false;
            }
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