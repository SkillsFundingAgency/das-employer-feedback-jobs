using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;
using SFA.DAS.EmployerFeedback.Jobs.Services;
using SFA.DAS.Encoding;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class ProcessFeedbackTransactionsEmailsFunction : BaseFunction<ProcessFeedbackTransactionsEmailsFunction>
    {
        private readonly IEmployerFeedbackOuterApi _api;
        private readonly ApplicationConfiguration _configuration;
        private readonly IWaveFanoutService _waveFanoutService;
        private readonly IEncodingService _encodingService;

        public ProcessFeedbackTransactionsEmailsFunction(
            ILogger<ProcessFeedbackTransactionsEmailsFunction> logger,
            IEmployerFeedbackOuterApi api,
            ApplicationConfiguration configuration,
            IWaveFanoutService waveFanoutService,
            IEncodingService encodingService) : base(logger)
        {
            _api = api;
            _configuration = configuration;
            _waveFanoutService = waveFanoutService;
            _encodingService = encodingService;
        }

        [Function(nameof(ProcessFeedbackTransactionsEmailsTimer))]
        public async Task ProcessFeedbackTransactionsEmailsTimer([TimerTrigger("%ProcessFeedbackTransactionsEmailsSchedule%", RunOnStartup = false)] TimerInfo timer)
        {
            try
            {
                Logger.LogInformation("ProcessFeedbackTransactionsEmails started");

                var response = await ExecuteWithRetry(async () =>
                {
                    Logger.LogInformation("Fetching feedback transactions to email");
                    return await _api.GetFeedbackTransactionsBatch(_configuration.ProcessFeedbackEmailsBatchSize);
                }, MaxRetryAttempts, CancellationToken.None);

                var transactionIds = response.FeedbackTransactions;

                Logger.LogInformation("Processing {Count} feedback transactions sequentially", transactionIds.Count);

                var results = new List<bool>();
                foreach (var transactionId in transactionIds)
                {
                    var result = await ProcessSingleFeedbackTransactionAsync(transactionId);
                    results.Add(result);
                }

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

        private async Task<bool> ProcessSingleFeedbackTransactionAsync(long transactionId)
        {
            try
            {
                Logger.LogInformation("Starting processing for feedback transaction {TransactionId}", transactionId);

                var usersResponse = await ExecuteWithRetry(async () =>
                {
                    Logger.LogInformation("Getting users for feedback transaction {TransactionId}", transactionId);
                    return await _api.GetFeedbackTransactionUsers(transactionId);
                }, MaxRetryAttempts, CancellationToken.None);

                if (usersResponse.Users == null || !usersResponse.Users.Any())
                {
                    Logger.LogInformation("No users found for feedback transaction {TransactionId}", transactionId);
                    await UpdateFeedbackTransactionAsync(transactionId, usersResponse.TemplateName, 0);
                    return true;
                }

                Logger.LogInformation("Found {UserCount} users for feedback transaction {TransactionId}",
                    usersResponse.Users.Count, transactionId);

                var templateId = GetTemplateIdFromName(usersResponse.TemplateName);

                if (templateId == Guid.Empty)
                {
                    Logger.LogError("Template not found for name: {TemplateName}", usersResponse.TemplateName);
                    return false;
                }

                var accountHashedId = _encodingService.Encode(usersResponse.AccountId, Encoding.EncodingType.AccountId);

                var emailRequests = usersResponse.Users.Select(user => new SendFeedbackEmailRequest
                {
                    TemplateId = templateId,
                    Contact = user.Name,
                    Email = user.Email,
                    EmployerName = usersResponse.AccountName,
                    AccountHashedId = accountHashedId,
                    AccountsBaseUrl = _configuration.EmployerAccountsBaseUrl,
                    FeedbackBaseUrl = _configuration.EmployerFeedbackBaseUrl
                }).ToList();

                Logger.LogInformation("Sending {EmailCount} emails for transaction {TransactionId} with throttling (max 10 per second)",
                    emailRequests.Count, transactionId);

                var emailResults = await _waveFanoutService.ExecuteAsync(
                    emailRequests,
                    SendSingleEmailAsync,
                   _configuration.ProcessFeedbackEmailsMaxParallelism,
                    1000);

                var sentCount = emailResults.Count(r => r);
                var failedCount = emailResults.Count - sentCount;

                Logger.LogInformation("Email sending completed for transaction {TransactionId}: {SentCount} sent, {FailedCount} failed",
                    transactionId, sentCount, failedCount);

                await UpdateFeedbackTransactionAsync(transactionId, usersResponse.TemplateName, sentCount);

                Logger.LogInformation("Successfully completed processing for feedback transaction {TransactionId}. Sent {SentCount} emails",
                    transactionId, sentCount);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Processing failed for feedback transaction {TransactionId}: {ErrorMessage}",
                    transactionId, ex.Message);
                return false;
            }
        }

        private async Task<bool> SendSingleEmailAsync(SendFeedbackEmailRequest emailRequest)
        {
            try
            {
                await ExecuteWithRetry(async () =>
                {
                    Logger.LogInformation("Sending email to {Contact} with template {TemplateId}",
                        emailRequest.Contact, emailRequest.TemplateId);
                    await _api.SendFeedbackEmail(emailRequest);
                    return true;
                }, MaxRetryAttempts, CancellationToken.None);

                Logger.LogInformation("Successfully sent email to {Contact}", emailRequest.Contact);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send email to {Contact}: {ErrorMessage}",
                    emailRequest.Contact, ex.Message);
                return false;
            }
        }

        private async Task UpdateFeedbackTransactionAsync(long transactionId, string templateName, int sentCount)
        {
            try
            {
                var templateId = GetTemplateIdFromName(templateName);

                var updateRequest = new UpdateFeedbackTransactionRequest
                {
                    TemplateId = templateId,
                    SentCount = sentCount,
                    SentDate = DateTime.UtcNow
                };

                await ExecuteWithRetry(async () =>
                {
                    Logger.LogInformation("Updating feedback transaction {TransactionId} with sent count {SentCount}",
                        transactionId, sentCount);
                    await _api.UpdateFeedbackTransaction(transactionId, updateRequest);
                    return true;
                }, MaxRetryAttempts, CancellationToken.None);

                Logger.LogInformation("Successfully updated feedback transaction {TransactionId}", transactionId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to update feedback transaction {TransactionId}: {ErrorMessage}",
                    transactionId, ex.Message);
                throw;
            }
        }

        private Guid GetTemplateIdFromName(string templateName)
        {
            var template = _configuration.NotificationTemplates
                .FirstOrDefault(t => t.TemplateName.Equals(templateName, StringComparison.OrdinalIgnoreCase));

            return template?.TemplateId ?? Guid.Empty;
        }
    }
}