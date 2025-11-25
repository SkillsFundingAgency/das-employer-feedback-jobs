using System.Diagnostics.CodeAnalysis;

namespace SFA.DAS.EmployerFeedback.Infrastructure.Configuration
{
    [ExcludeFromCodeCoverage]
    public class ApplicationConfiguration
    {
        public EmployerFeedbackOuterApiConfiguration EmployerFeedbackOuterApiConfiguration { get; set; }
        public int GenerateFeedbackTransactionsMaxParallelism { get; set; }
        public int GenerateFeedbackTransactionsBatchSize { get; set; }
    }
}
