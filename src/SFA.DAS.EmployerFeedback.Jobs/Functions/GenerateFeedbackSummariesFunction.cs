using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class GenerateFeedbackSummariesFunction
    {
        private readonly ILogger<SyncEmployerAccountsFunction> _logger;
        private readonly IEmployerFeedbackOuterApi _api;

        public GenerateFeedbackSummariesFunction(ILogger<SyncEmployerAccountsFunction> log, IEmployerFeedbackOuterApi api)
        {
            _logger = log;
            _api = api;
        }

        [Function(nameof(GenerateFeedbackSummariesFunctionTimer))]
        public async Task GenerateFeedbackSummariesFunctionTimer([TimerTrigger("%GenerateFeedbackSummariesFunctionTimerSchedule%", RunOnStartup = false)] TimerInfo timer)
        {
            await Run(nameof(GenerateFeedbackSummariesFunctionTimer));
        }
        private async Task Run(string functionName)
        {
            try
            {
                _logger.LogInformation("GenerateFeedbackSummariesFunctionTimer has started");
                await _api.GenerateFeedbackSummaries();
                _logger.LogInformation("GenerateFeedbackSummariesFunctionTimer has finished");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateFeedbackSummariesFunctionTimer has failed");
                throw;
            }
        }
    }
}
