namespace SFA.DAS.EmployerFeedback.Infrastructure.Models
{
    public class GetFeedbackTransactionsBatchResponse
    {
        public List<long> FeedbackTransactions { get; set; } = new List<long>();
    }
}