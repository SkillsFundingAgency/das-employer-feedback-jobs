using RestEase;

namespace SFA.DAS.EmployerFeedback.Infrastructure.Api
{
    public interface IEmployerFeedbackOuterApi
    {
        [Post("/account/update")]
        Task SyncEmployerAccounts();

        [Post("dataload/generate-feedback-summaries")]
        Task GenerateFeedbackSummaries();
    }
}
