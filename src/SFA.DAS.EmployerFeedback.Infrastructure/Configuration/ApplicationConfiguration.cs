using System.Diagnostics.CodeAnalysis;

namespace SFA.DAS.EmployerFeedback.Infrastructure.Configuration
{
    [ExcludeFromCodeCoverage]
    public class ApplicationConfiguration
    {
        public EmployerFeedbackOuterApiConfiguration EmployerFeedbackOuterApiConfiguration { get; set; }
        public int GenerateFeedbackTransactionsMaxParallelism { get; set; }
        public int GenerateFeedbackTransactionsBatchSize { get; set; }
        public int ProcessFeedbackEmailsBatchSize { get; set; }
        public int ProcessFeedbackEmailsDelayMs { get; set; } = 100;
        public List<NotificationTemplate> NotificationTemplates { get; set; } = new List<NotificationTemplate>();
        public string EmployerAccountsBaseUrl { get; set; } = null!;
        public string EmployerFeedbackBaseUrl { get; set; } = null!;
    }
}
