using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class GenerateFeedbackSummariesFunction : BaseFunction<GenerateFeedbackSummariesFunction>
    {
        private readonly IEmployerFeedbackOuterApi _api;

        public GenerateFeedbackSummariesFunction(
            ILogger<GenerateFeedbackSummariesFunction> logger,
            IEmployerFeedbackOuterApi api) : base(logger)
        {
            _api = api;
        }

        [Function(nameof(GenerateFeedbackSummariesFunctionTimer))]
        public async Task GenerateFeedbackSummariesFunctionTimer([TimerTrigger("%GenerateFeedbackSummariesFunctionTimerSchedule%", RunOnStartup = false)] TimerInfo timer)
        {
            try
            {
                Logger.LogInformation("GenerateFeedbackSummariesFunctionTimer has started");
                await _api.GenerateFeedbackSummaries();
                Logger.LogInformation("GenerateFeedbackSummariesFunctionTimer has finished");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GenerateFeedbackSummariesFunctionTimer has failed");
                throw;
            }
        }
    }
}
