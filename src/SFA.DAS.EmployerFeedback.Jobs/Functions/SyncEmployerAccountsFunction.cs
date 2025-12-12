using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;

namespace SFA.DAS.EmployerFeedback.Jobs.Functions
{
    public class SyncEmployerAccountsFunction : BaseFunction<SyncEmployerAccountsFunction>
    {
        private readonly IEmployerFeedbackOuterApi _api;

        public SyncEmployerAccountsFunction(
            ILogger<SyncEmployerAccountsFunction> logger, 
            IEmployerFeedbackOuterApi api) : base(logger)
        {
            _api = api;
        }

        [Function(nameof(SyncEmployerAccountsTimer))]
        public async Task SyncEmployerAccountsTimer([TimerTrigger("%SyncEmployerAccountsTimerSchedule%", RunOnStartup = false)] TimerInfo timer)
        {
            try
            {
                Logger.LogInformation("SyncEmployerAccountsTimer has started");

                await _api.SyncEmployerAccounts();

                Logger.LogInformation("SyncEmployerAccountsTimer has finished");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SyncEmployerAccountsTimer has failed");
                throw;
            }
        }
    }
}
