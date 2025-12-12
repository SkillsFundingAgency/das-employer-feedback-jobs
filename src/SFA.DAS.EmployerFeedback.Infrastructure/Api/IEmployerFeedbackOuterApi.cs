using RestEase;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;

namespace SFA.DAS.EmployerFeedback.Infrastructure.Api
{
    public interface IEmployerFeedbackOuterApi
    {
        [Post("/accounts/update")]
        Task SyncEmployerAccounts();

        [Post("dataload/generate-feedback-summaries")]
        Task GenerateFeedbackSummaries();

        [Get("/accounts")]
        Task<GetFeedbackTransactionAccountIdsResponse> GetFeedbackTransactionAccountIds([Query] int batchsize);

        [Post("/accounts/{accountId}/feedbacktransaction")]
        Task ProcessFeedbackTransactionForAccount([Path] string accountId);

        [Get("/feedbacktransactions")]
        Task<GetFeedbackTransactionsBatchResponse> GetFeedbackTransactionsBatch([Query] int batchsize);

        [Get("/feedbacktransactions/{id}/users")]
        Task<GetFeedbackTransactionUsersResponse> GetFeedbackTransactionUsers([Path] long id);

        [Post("/feedbacktransactions/send")]
        Task SendFeedbackEmail([Body] SendFeedbackEmailRequest request);

        [Put("/feedbacktransactions/{id}")]
        Task UpdateFeedbackTransaction([Path] long id, [Body] UpdateFeedbackTransactionRequest request);
    }
}
