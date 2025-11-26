using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;
using SFA.DAS.EmployerFeedback.Jobs.Services;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class ProcessFeedbackTransactionsEmailsFunction : BaseFunction<ProcessFeedbackTransactionsEmailsFunction>
    {
        private readonly IEmployerFeedbackOuterApi _api;
        private readonly ApplicationConfiguration _configuration;
        private readonly IWaveFanoutService _waveFanoutService;

        public ProcessFeedbackTransactionsEmailsFunction(
            ILogger<ProcessFeedbackTransactionsEmailsFunction> logger,
            IEmployerFeedbackOuterApi api,
            ApplicationConfiguration configuration,
            IWaveFanoutService waveFanoutService) : base(logger)
        {
            _api = api;
            _configuration = configuration;
            _waveFanoutService = waveFanoutService;
        }

        [Function(nameof(ProcessFeedbackTransactionsEmailsTimer))]
        public async Task ProcessFeedbackTransactionsEmailsTimer([TimerTrigger("%ProcessFeedbackTransactionsEmailsSchedule%", RunOnStartup = false)] TimerInfo timer)
        {
            try
            {
                Logger.LogInformation("ProcessFeedbackTransactionsEmails started");

                var response = await ExecuteWithRetry(async () =>
                {
                    Logger.LogDebug("Fetching feedback transactions to email");
                    return await _api.GetFeedbackTransactionsBatch(_configuration.ProcessFeedbackEmailsBatchSize);
                }, MaxRetryAttempts, CancellationToken.None);

                var transactionIds = response.FeedbackTransactions;

                Logger.LogInformation("Processing {Count} feedback transactions with wave fanout (max {MaxParallelism} per second)",
                    transactionIds.Count, _configuration.ProcessFeedbackEmailsMaxParallelism);

                var results = await _waveFanoutService.ExecuteAsync(
                    transactionIds,
                    ProcessSingleEmailAsync,
                    _configuration.ProcessFeedbackEmailsMaxParallelism,
                    1000);

                var successCount = results.Count(r => r);
                var failureCount = results.Count - successCount;

                Logger.LogInformation("ProcessFeedbackTransactionsEmails completed: {SuccessCount} successful, {FailureCount} failed",
                    successCount, failureCount);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ProcessFeedbackTransactionsEmails failed");
                throw;
            }
        }

        private async Task<bool> ProcessSingleEmailAsync(long transactionId)
        {
            try
            {
                Logger.LogDebug("Starting email processing for transaction {TransactionId}", transactionId);

                await ExecuteWithRetry(async () =>
                {
                    var request = new SendFeedbackEmailsRequest
                    {
                        NotificationTemplates = _configuration.NotificationTemplates,
                        EmployerAccountsBaseUrl = _configuration.EmployerAccountsBaseUrl,
                        EmployerFeedbackBaseUrl = _configuration.EmployerFeedbackBaseUrl
                    };

                    Logger.LogDebug("Sending email for transaction {TransactionId} with templates: {@Templates} and base URL: {BaseUrl}",
                        transactionId, request.NotificationTemplates, request.EmployerAccountsBaseUrl);

                    await _api.SendFeedbackEmails(transactionId, request);

                    Logger.LogDebug("Successfully sent email for transaction {TransactionId}", transactionId);

                    return true;
                }, MaxRetryAttempts, CancellationToken.None);

                Logger.LogInformation("Successfully completed email processing for transaction {TransactionId}", transactionId);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Email processing failed for transaction {TransactionId}: {ErrorMessage}", 
                    transactionId, ex.Message);
                return false;
            }
        }
    }
}