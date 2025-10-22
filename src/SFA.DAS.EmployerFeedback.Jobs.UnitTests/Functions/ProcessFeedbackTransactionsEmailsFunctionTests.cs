using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Infrastructure.Configuration;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;
using SFA.DAS.EmployerFeedback.Jobs.Functions;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Functions
{
    [TestFixture]
    public class ProcessFeedbackTransactionsEmailsFunctionTests
    {
        private Mock<ILogger<ProcessFeedbackTransactionsEmailsFunction>> _loggerMock;
        private Mock<IEmployerFeedbackOuterApi> _apiMock;
        private ApplicationConfiguration _configuration;
        private ProcessFeedbackTransactionsEmailsFunction _function;
        private const int MaxRetryAttempts = 3;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<ProcessFeedbackTransactionsEmailsFunction>>();
            _apiMock = new Mock<IEmployerFeedbackOuterApi>();
            _configuration = new ApplicationConfiguration
            {
                TriggerFeedbackEmailsBatchSize = 25,
                TriggerFeedbackEmailsMaxParallelism = 10,
                NotificationTemplates = new List<NotificationTemplate>
                {
                    new NotificationTemplate
                    {
                        TemplateName = "DefaultEmployerFeedbackRequestTemplate",
                        TemplateId = Guid.Parse("a0d17e87-3b0c-49cb-98f8-024fc6d256a5")
                    }
                },
                EmployerAccountsBaseUrl = "at-eas.apprenticeships.education.gov.uk",
                EmployerFeedbackBaseUrl = "at-employer-feedback.apprenticeships.education.gov.uk"
            };
            _function = new ProcessFeedbackTransactionsEmailsFunction(_loggerMock.Object, _apiMock.Object, _configuration);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_CallsApiAndProcessesEmails()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long> { 1, 2, 3 };
            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.TriggerFeedbackEmails(It.IsAny<long>(), It.IsAny<TriggerFeedbackEmailsRequest>()))
                   .Returns(Task.CompletedTask);

            await _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo);

            _apiMock.Verify(x => x.GetFeedbackTransactionsBatch(_configuration.TriggerFeedbackEmailsBatchSize), Times.Once);
            _apiMock.Verify(x => x.TriggerFeedbackEmails(It.IsAny<long>(), It.IsAny<TriggerFeedbackEmailsRequest>()), Times.Exactly(3));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("ProcessFeedbackTransactionsEmails completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_WhenGetFeedbackTransactionsThrows_RetriesAndSucceeds()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long> { 1 };
            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };

            _apiMock.SetupSequence(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>()))
                .ThrowsAsync(new HttpRequestException("Temporary error"))
                .ReturnsAsync(response);
            _apiMock.Setup(x => x.TriggerFeedbackEmails(It.IsAny<long>(), It.IsAny<TriggerFeedbackEmailsRequest>()))
                   .Returns(Task.CompletedTask);

            await _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo);

            _apiMock.Verify(x => x.GetFeedbackTransactionsBatch(_configuration.TriggerFeedbackEmailsBatchSize), Times.Exactly(2));
            _apiMock.Verify(x => x.TriggerFeedbackEmails(1, It.IsAny<TriggerFeedbackEmailsRequest>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retrying operation") && v.ToString().Contains("attempt 2 of 3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_ProcessesInBatchesOf10()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long>();

            for (int i = 1; i <= 25; i++)
            {
                transactionIds.Add(i);
            }

            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.TriggerFeedbackEmails(It.IsAny<long>(), It.IsAny<TriggerFeedbackEmailsRequest>()))
                   .Returns(Task.CompletedTask);

            await _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo);

            _apiMock.Verify(x => x.TriggerFeedbackEmails(It.IsAny<long>(), It.IsAny<TriggerFeedbackEmailsRequest>()), Times.Exactly(25));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing 25 feedback transactions in batches of 10")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing batch 1 (10 transactions)")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing batch 3 (5 transactions)")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_WhenProcessingEmailFails_ContinuesWithOtherEmails()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long> { 1, 2, 3 };
            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.TriggerFeedbackEmails(2, It.IsAny<TriggerFeedbackEmailsRequest>()))
                .ThrowsAsync(new Exception("Email sending failed"));
            _apiMock.Setup(x => x.TriggerFeedbackEmails(It.Is<long>(id => id != 2), It.IsAny<TriggerFeedbackEmailsRequest>()))
                   .Returns(Task.CompletedTask);

            await _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo);

            _apiMock.Verify(x => x.TriggerFeedbackEmails(1, It.IsAny<TriggerFeedbackEmailsRequest>()), Times.Once);
            _apiMock.Verify(x => x.TriggerFeedbackEmails(2, It.IsAny<TriggerFeedbackEmailsRequest>()), Times.Exactly(MaxRetryAttempts));
            _apiMock.Verify(x => x.TriggerFeedbackEmails(3, It.IsAny<TriggerFeedbackEmailsRequest>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("ProcessFeedbackTransactionsEmails completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to send email for transaction 2") && v.ToString().Contains("after 3 attempts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void ProcessFeedbackTransactionsEmailsTimer_WhenGetFeedbackTransactionsFails_ThrowsException()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var exception = new Exception("API failed");

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ThrowsAsync(exception);

            var ex = Assert.ThrowsAsync<Exception>(() => _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo));
            Assert.That(ex, Is.EqualTo(exception));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }
    }
}