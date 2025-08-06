using RestEase;

namespace SFA.DAS.EmployerFeedback.Infrastructure.Api
{
    // TODO:
    // This endpoint currently points to '/attributes' only to test connectivity with the Outer API.
    // This should be updated to the correct endpoint as part of the next ticket.
    public interface IEmployerFeedbackOuterApi
    {
        [Get("/attributes")]
        Task SyncEmployerAccounts();
    }
}
