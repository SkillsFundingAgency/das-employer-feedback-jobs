using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class GenerateFeedbackSummariesFunction
    {
        private readonly ILogger<GenerateFeedbackSummariesFunction> _logger;
        private readonly IEmployerFeedbackOuterApi _api;

        public GenerateFeedbackSummariesFunction(ILogger<GenerateFeedbackSummariesFunction> log, IEmployerFeedbackOuterApi api)
        {
            _logger = log;
            _api = api;
        }

        [Function(nameof(GenerateFeedbackSummariesFunctionTimer))]
        public async Task GenerateFeedbackSummariesFunctionTimer([TimerTrigger("%GenerateFeedbackSummariesFunctionTimerSchedule%", RunOnStartup = false)] TimerInfo timer)
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
