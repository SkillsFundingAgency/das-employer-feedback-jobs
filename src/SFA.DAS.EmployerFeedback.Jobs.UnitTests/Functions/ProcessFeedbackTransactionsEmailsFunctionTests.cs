using System;
using System.Collections.Generic;
using System.Linq;
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
using SFA.DAS.EmployerFeedback.Jobs.Services;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Functions
{
    [TestFixture]
    public class ProcessFeedbackTransactionsEmailsFunctionTests
    {
        private Mock<ILogger<ProcessFeedbackTransactionsEmailsFunction>> _loggerMock;
        private Mock<IEmployerFeedbackOuterApi> _apiMock;
        private Mock<IWaveFanoutService> _waveFanoutServiceMock;
        private ApplicationConfiguration _configuration;
        private ProcessFeedbackTransactionsEmailsFunction _function;
        private const int MaxRetryAttempts = 3;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<ProcessFeedbackTransactionsEmailsFunction>>();
            _apiMock = new Mock<IEmployerFeedbackOuterApi>();
            _waveFanoutServiceMock = new Mock<IWaveFanoutService>();
            _configuration = new ApplicationConfiguration
            {
                ProcessFeedbackEmailsBatchSize = 25,
                ProcessFeedbackEmailsMaxParallelism = 10,
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
            _function = new ProcessFeedbackTransactionsEmailsFunction(_loggerMock.Object, _apiMock.Object, _configuration, _waveFanoutServiceMock.Object);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_CallsApiAndProcessesEmails()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long> { 1, 2, 3 };
            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.SendFeedbackEmails(It.IsAny<long>(), It.IsAny<SendFeedbackEmailsRequest>()))
                   .Returns(Task.CompletedTask);

            _waveFanoutServiceMock.Setup(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<long>>(),
                It.IsAny<Func<long, Task<bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
                .Returns<IEnumerable<long>, Func<long, Task<bool>>, int, int>(
                    async (items, func, perSecondCap, delay) =>
                    {
                        var results = new List<bool>();
                        foreach (var item in items)
                        {
                            var result = await func(item);
                            results.Add(result);
                        }
                        return results.AsReadOnly();
                    });

            await _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo);

            _apiMock.Verify(x => x.GetFeedbackTransactionsBatch(_configuration.ProcessFeedbackEmailsBatchSize), Times.Once);

            _apiMock.Verify(x => x.SendFeedbackEmails(It.IsAny<long>(), It.IsAny<SendFeedbackEmailsRequest>()), Times.Exactly(3));
            _apiMock.Verify(x => x.SendFeedbackEmails(1, It.IsAny<SendFeedbackEmailsRequest>()), Times.Once);
            _apiMock.Verify(x => x.SendFeedbackEmails(2, It.IsAny<SendFeedbackEmailsRequest>()), Times.Once);
            _apiMock.Verify(x => x.SendFeedbackEmails(3, It.IsAny<SendFeedbackEmailsRequest>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("completed: 3 successful, 0 failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_WhenGetFeedbackTransactionsThrows_RetriesAndSucceeds()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long> { 1 };
            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };
            var expectedResults = new List<bool> { true }.AsReadOnly();

            _apiMock.SetupSequence(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>()))
                .ThrowsAsync(new HttpRequestException("Temporary error"))
                .ReturnsAsync(response);

            _waveFanoutServiceMock.Setup(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<long>>(),
                It.IsAny<Func<long, Task<bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
                .ReturnsAsync(expectedResults);

            await _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo);

            _apiMock.Verify(x => x.GetFeedbackTransactionsBatch(_configuration.ProcessFeedbackEmailsBatchSize), Times.Exactly(2));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retrying operation") && v.ToString().Contains("attempt 2 of 3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_ProcessesWithWaveFanout()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long>();

            for (int i = 1; i <= 25; i++)
            {
                transactionIds.Add(i);
            }

            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.SendFeedbackEmails(It.IsAny<long>(), It.IsAny<SendFeedbackEmailsRequest>()))
                   .Returns(Task.CompletedTask);

            _waveFanoutServiceMock.Setup(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<long>>(),
                It.IsAny<Func<long, Task<bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
                .Returns<IEnumerable<long>, Func<long, Task<bool>>, int, int>(
                    async (items, func, perSecondCap, delay) =>
                    {
                        var results = new List<bool>();
                        foreach (var item in items)
                        {
                            var result = await func(item);
                            results.Add(result);
                        }
                        return results.AsReadOnly();
                    });

            await _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo);

            _waveFanoutServiceMock.Verify(x => x.ExecuteAsync(
                It.Is<IEnumerable<long>>(ids => ids.SequenceEqual(transactionIds)),
                It.IsAny<Func<long, Task<bool>>>(),
                _configuration.ProcessFeedbackEmailsMaxParallelism,
                1000), Times.Once);

            _apiMock.Verify(x => x.SendFeedbackEmails(It.IsAny<long>(), It.IsAny<SendFeedbackEmailsRequest>()), Times.Exactly(25));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing 25 feedback transactions with wave fanout (max 10 per second)")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_WhenProcessingEmailFails_LogsErrorAndContinues()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long> { 1, 2, 3 };
            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);

            _apiMock.Setup(x => x.SendFeedbackEmails(2, It.IsAny<SendFeedbackEmailsRequest>()))
                .ThrowsAsync(new Exception("Email sending failed"));
            _apiMock.Setup(x => x.SendFeedbackEmails(It.Is<long>(id => id != 2), It.IsAny<SendFeedbackEmailsRequest>()))
                   .Returns(Task.CompletedTask);

            _waveFanoutServiceMock.Setup(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<long>>(),
                It.IsAny<Func<long, Task<bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
                .Returns<IEnumerable<long>, Func<long, Task<bool>>, int, int>(
                    async (items, func, perSecondCap, delay) =>
                    {
                        var results = new List<bool>();
                        foreach (var item in items)
                        {
                            var result = await func(item);
                            results.Add(result);
                        }
                        return results.AsReadOnly();
                    });

            await _function.ProcessFeedbackTransactionsEmailsTimer(timerInfo);

            _apiMock.Verify(x => x.SendFeedbackEmails(1, It.IsAny<SendFeedbackEmailsRequest>()), Times.Once);
            _apiMock.Verify(x => x.SendFeedbackEmails(3, It.IsAny<SendFeedbackEmailsRequest>()), Times.Once);


            _apiMock.Verify(x => x.SendFeedbackEmails(2, It.IsAny<SendFeedbackEmailsRequest>()), Times.Exactly(MaxRetryAttempts));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("completed: 2 successful, 1 failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Email processing failed for transaction 2")),
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