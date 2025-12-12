namespace SFA.DAS.EmployerFeedback.Infrastructure.Models
{
    public class GetFeedbackTransactionUsersResponse
    {
        public long AccountId { get; set; }
        public string AccountName { get; set; } = null!;
        public string TemplateName { get; set; } = null!;
        public List<FeedbackUser> Users { get; set; } = new List<FeedbackUser>();
    }

    public class FeedbackUser
    {
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
    }
}