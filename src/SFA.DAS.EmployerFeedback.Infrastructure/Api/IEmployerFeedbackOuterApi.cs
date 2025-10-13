using RestEase;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;

namespace SFA.DAS.EmployerFeedback.Infrastructure.Api
{
    public interface IEmployerFeedbackOuterApi
    {
        [Post("/account/update")]
        Task SyncEmployerAccounts();

        [Post("dataload/generate-feedback-summaries")]
        Task GenerateFeedbackSummaries();

        [Get("/account")]
        Task<GetEmployerAccountIdsResponse> GetEmployerAccountIds([Query] int batchsize);

        [Post("/account/{accountId}/feedbacktransaction")]
        Task ProcessFeedbackTransactionForAccount([Path] string accountId);
    }
}
