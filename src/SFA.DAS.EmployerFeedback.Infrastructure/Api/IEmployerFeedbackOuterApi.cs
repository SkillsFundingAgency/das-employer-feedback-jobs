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
        Task<GetFeedbackTransactionAccountIdsResponse> GetFeedbackTransactionAccountIds([Query] int batchsize);

        [Post("/account/{accountId}/feedbacktransaction")]
        Task ProcessFeedbackTransactionForAccount([Path] string accountId);

        [Get("/feedbacktransactions")]
        Task<GetFeedbackTransactionsBatchResponse> GetFeedbackTransactionsBatch([Query] int batchsize);

        [Post("/feedbacktransactions/{id}/send")]
        Task SendFeedbackEmails([Path] long id, [Body] SendFeedbackEmailsRequest request);
    }
}
