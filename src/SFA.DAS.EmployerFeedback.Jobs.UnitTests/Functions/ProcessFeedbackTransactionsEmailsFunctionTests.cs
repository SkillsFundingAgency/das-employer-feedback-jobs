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
using SFA.DAS.EmployerFeedback.Jobs.Services;
using SFA.DAS.Encoding;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Functions
{
    [TestFixture]
    public class ProcessFeedbackTransactionsEmailsFunctionTests
    {
        private Mock<ILogger<ProcessFeedbackTransactionsEmailsFunction>> _loggerMock;
        private Mock<IEmployerFeedbackOuterApi> _apiMock;
        private Mock<IWaveFanoutService> _waveFanoutServiceMock;
        private Mock<IEncodingService> _encodingServiceMock;
        private ApplicationConfiguration _configuration;
        private ProcessFeedbackTransactionsEmailsFunction _function;
        private const int MaxRetryAttempts = 3;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<ProcessFeedbackTransactionsEmailsFunction>>();
            _apiMock = new Mock<IEmployerFeedbackOuterApi>();
            _waveFanoutServiceMock = new Mock<IWaveFanoutService>();
            _encodingServiceMock = new Mock<IEncodingService>();

            _configuration = new ApplicationConfiguration
            {
                ProcessFeedbackEmailsBatchSize = 25,
                ProcessFeedbackEmailsPerSecondCap = 10,
                NotificationTemplates = new List<NotificationTemplate>
                {
                    new NotificationTemplate
                    {
                        TemplateName = "EmployerFeedbackRequest",
                        TemplateId = Guid.Parse("a0d17e87-3b0c-49cb-98f8-024fc6d256a5")
                    }
                },
                EmployerAccountsBaseUrl = "at-eas.apprenticeships.education.gov.uk",
                EmployerFeedbackBaseUrl = "at-employer-feedback.apprenticeships.education.gov.uk"
            };

            _encodingServiceMock.Setup(x => x.Encode(It.IsAny<long>(), It.IsAny<EncodingType>())).Returns("ABC123");

            _function = new ProcessFeedbackTransactionsEmailsFunction(
                _loggerMock.Object,
                _apiMock.Object,
                _configuration,
                _waveFanoutServiceMock.Object,
                _encodingServiceMock.Object);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_CallsApiAndProcessesEmails()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long> { 1, 2, 3 };
            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };

            var usersResponse = new GetFeedbackTransactionUsersResponse
            {
                AccountId = 12345,
                AccountName = "Test Company",
                TemplateName = "EmployerFeedbackRequest",
                Users = new List<FeedbackUser>
                {
                    new FeedbackUser { Name = "John Doe", Email = "john@test.com" },
                    new FeedbackUser { Name = "Jane Smith", Email = "jane@test.com" }
                }
            };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.GetFeedbackTransactionUsers(It.IsAny<long>())).ReturnsAsync(usersResponse);
            _apiMock.Setup(x => x.SendFeedbackEmail(It.IsAny<SendFeedbackEmailRequest>())).Returns(Task.CompletedTask);
            _apiMock.Setup(x => x.UpdateFeedbackTransaction(It.IsAny<long>(), It.IsAny<UpdateFeedbackTransactionRequest>())).Returns(Task.CompletedTask);

            _waveFanoutServiceMock.Setup(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<SendFeedbackEmailRequest>>(),
                It.IsAny<Func<SendFeedbackEmailRequest, Task<bool>>>(),
                _configuration.ProcessFeedbackEmailsPerSecondCap,
                1000))
                .Returns<IEnumerable<SendFeedbackEmailRequest>, Func<SendFeedbackEmailRequest, Task<bool>>, int, int>(
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
            _apiMock.Verify(x => x.GetFeedbackTransactionUsers(It.IsAny<long>()), Times.Exactly(3));

            _apiMock.Verify(x => x.SendFeedbackEmail(It.IsAny<SendFeedbackEmailRequest>()), Times.Exactly(6));

            _apiMock.Verify(x => x.UpdateFeedbackTransaction(It.IsAny<long>(), It.IsAny<UpdateFeedbackTransactionRequest>()), Times.Exactly(3));

            _waveFanoutServiceMock.Verify(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<SendFeedbackEmailRequest>>(),
                It.IsAny<Func<SendFeedbackEmailRequest, Task<bool>>>(),
                _configuration.ProcessFeedbackEmailsPerSecondCap,
                1000), Times.Exactly(3));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing 3 feedback transactions sequentially")),
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

            var usersResponse = new GetFeedbackTransactionUsersResponse
            {
                AccountId = 12345,
                AccountName = "Test Company",
                TemplateName = "EmployerFeedbackRequest",
                Users = new List<FeedbackUser>
                {
                    new FeedbackUser { Name = "John Doe", Email = "john@test.com" }
                }
            };

            _apiMock.SetupSequence(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>()))
                .ThrowsAsync(new HttpRequestException("Temporary error"))
                .ReturnsAsync(response);

            _apiMock.Setup(x => x.GetFeedbackTransactionUsers(It.IsAny<long>())).ReturnsAsync(usersResponse);
            _apiMock.Setup(x => x.SendFeedbackEmail(It.IsAny<SendFeedbackEmailRequest>())).Returns(Task.CompletedTask);
            _apiMock.Setup(x => x.UpdateFeedbackTransaction(It.IsAny<long>(), It.IsAny<UpdateFeedbackTransactionRequest>())).Returns(Task.CompletedTask);

            _waveFanoutServiceMock.Setup(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<SendFeedbackEmailRequest>>(),
                It.IsAny<Func<SendFeedbackEmailRequest, Task<bool>>>(),
                _configuration.ProcessFeedbackEmailsPerSecondCap,
                1000))
                .ReturnsAsync(new List<bool> { true }.AsReadOnly());

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
            var usersResponse = new GetFeedbackTransactionUsersResponse
            {
                AccountId = 12345,
                AccountName = "Test Company",
                TemplateName = "EmployerFeedbackRequest",
                Users = new List<FeedbackUser>
                {
                    new FeedbackUser { Name = "John Doe", Email = "john@test.com" }
                }
            };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.GetFeedbackTransactionUsers(It.IsAny<long>())).ReturnsAsync(usersResponse);
            _apiMock.Setup(x => x.SendFeedbackEmail(It.IsAny<SendFeedbackEmailRequest>())).Returns(Task.CompletedTask);
            _apiMock.Setup(x => x.UpdateFeedbackTransaction(It.IsAny<long>(), It.IsAny<UpdateFeedbackTransactionRequest>())).Returns(Task.CompletedTask);

            _waveFanoutServiceMock.Setup(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<SendFeedbackEmailRequest>>(),
                It.IsAny<Func<SendFeedbackEmailRequest, Task<bool>>>(),
                _configuration.ProcessFeedbackEmailsPerSecondCap,
                1000))
                .Returns<IEnumerable<SendFeedbackEmailRequest>, Func<SendFeedbackEmailRequest, Task<bool>>, int, int>(
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
                It.IsAny<IEnumerable<SendFeedbackEmailRequest>>(),
                It.IsAny<Func<SendFeedbackEmailRequest, Task<bool>>>(),
                _configuration.ProcessFeedbackEmailsPerSecondCap,
                1000), Times.Exactly(25));

            _apiMock.Verify(x => x.SendFeedbackEmail(It.IsAny<SendFeedbackEmailRequest>()), Times.Exactly(25));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing 25 feedback transactions sequentially")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task ProcessFeedbackTransactionsEmailsTimer_WhenProcessingEmailFails_LogsErrorAndContinues()
        {
            var timerInfo = (TimerInfo)Activator.CreateInstance(typeof(TimerInfo), true);
            var transactionIds = new List<long> { 1, 2, 3 };
            var response = new GetFeedbackTransactionsBatchResponse { FeedbackTransactions = transactionIds };
            var usersResponse = new GetFeedbackTransactionUsersResponse
            {
                AccountId = 12345,
                AccountName = "Test Company",
                TemplateName = "EmployerFeedbackRequest",
                Users = new List<FeedbackUser>
                {
                    new FeedbackUser { Name = "John Doe", Email = "john@test.com" }
                }
            };

            _apiMock.Setup(x => x.GetFeedbackTransactionsBatch(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.GetFeedbackTransactionUsers(It.IsAny<long>())).ReturnsAsync(usersResponse);

            _apiMock.Setup(x => x.GetFeedbackTransactionUsers(2))
                .ThrowsAsync(new Exception("User retrieval failed"));

            _apiMock.Setup(x => x.SendFeedbackEmail(It.IsAny<SendFeedbackEmailRequest>())).Returns(Task.CompletedTask);
            _apiMock.Setup(x => x.UpdateFeedbackTransaction(It.IsAny<long>(), It.IsAny<UpdateFeedbackTransactionRequest>())).Returns(Task.CompletedTask);

            _waveFanoutServiceMock.Setup(x => x.ExecuteAsync(
                It.IsAny<IEnumerable<SendFeedbackEmailRequest>>(),
                It.IsAny<Func<SendFeedbackEmailRequest, Task<bool>>>(),
                _configuration.ProcessFeedbackEmailsPerSecondCap,
                1000))
                .Returns<IEnumerable<SendFeedbackEmailRequest>, Func<SendFeedbackEmailRequest, Task<bool>>, int, int>(
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

            _apiMock.Verify(x => x.SendFeedbackEmail(It.IsAny<SendFeedbackEmailRequest>()), Times.Exactly(2));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("completed: 2 successful, 1 failed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing failed for feedback transaction 2")),
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