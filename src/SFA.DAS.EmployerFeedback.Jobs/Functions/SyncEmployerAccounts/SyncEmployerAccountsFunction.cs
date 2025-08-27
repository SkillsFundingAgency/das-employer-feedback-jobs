using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions.SyncEmployerAccounts
{
    public class SyncEmployerAccountsFunction
    {
        private readonly ILogger<SyncEmployerAccountsFunction> _logger;
        private readonly IEmployerFeedbackOuterApi _api;

        public SyncEmployerAccountsFunction(ILogger<SyncEmployerAccountsFunction> log, IEmployerFeedbackOuterApi api)
        {
            _logger = log;
            _api = api;
        }

        // TODO:
        // SyncEmployerAccountsTimerSchedule needs to be added to template.json
        // once the DevOps work related to the pipeline setup and associated changes
        // to the template.json file has been completed.

        // TODO:
        // Test case for this Azure Function needs to be implemented
        // as part of the SyncEmployerAccounts Function ticket.
        [Function(nameof(SyncEmployerAccountsTimer))]
        public async Task SyncEmployerAccountsTimer([TimerTrigger("%SyncEmployerAccountsTimerSchedule%", RunOnStartup = true)] TimerInfo timer)
        {
            await Run(nameof(SyncEmployerAccountsTimer));
        }

        private async Task Run(string functionName)
        {
            try
            {
                _logger.LogInformation("{FunctionName} has started", functionName);

                await _api.SyncEmployerAccounts();

                _logger.LogInformation("{FunctionName} has finished", functionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{FunctionName} has failed", functionName);
                throw;
            }
        }
    }
}
