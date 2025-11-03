using Newtonsoft.Json;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;

namespace SFA.DAS.EmployerFeedback.Infrastructure.Models
{
    public class SendFeedbackEmailsRequest
    {
        public List<NotificationTemplate> NotificationTemplates { get; set; } = new List<NotificationTemplate>();

        public string EmployerAccountsBaseUrl { get; set; } = null!;
        public string EmployerFeedbackBaseUrl { get; set; } = null!;
    }
}