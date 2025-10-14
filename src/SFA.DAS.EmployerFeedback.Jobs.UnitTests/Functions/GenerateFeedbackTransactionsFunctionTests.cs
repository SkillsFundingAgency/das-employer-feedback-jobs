using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
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
    public class GenerateFeedbackTransactionsFunctionTests
    {
        private Mock<ILogger<GenerateFeedbackTransactionsFunction>> _loggerMock;
        private Mock<IEmployerFeedbackOuterApi> _apiMock;
        private ApplicationConfiguration _configuration;
        private GenerateFeedbackTransactionsFunction _function;
        private const int MaxRetryAttempts = 3;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<GenerateFeedbackTransactionsFunction>>();
            _apiMock = new Mock<IEmployerFeedbackOuterApi>();
            _configuration = new ApplicationConfiguration
            {
                GenerateFeedbackTransactionsMaxParallelism = 2,
                GenerateFeedbackTransactionsBatchSize = 2,
            };
            _function = new GenerateFeedbackTransactionsFunction(_loggerMock.Object, _apiMock.Object, _configuration);
        }

        [Test]
        public async Task GenerateFeedbackTransactionsTimer_CallsApiAndProcessesAccounts()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);
            var accountIds = new List<string> { "account1", "account2", "account3", "account4" };
            var response = new GetFeedbackTransactionAccountIdsResponse { AccountIds = accountIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionAccountIds(It.IsAny<int>())).ReturnsAsync(response);

            await _function.GenerateFeedbackTransactionsTimer(timerInfo);

            _apiMock.Verify(x => x.GetFeedbackTransactionAccountIds(It.IsAny<int>()), Times.Once);
            _apiMock.Verify(x => x.ProcessFeedbackTransactionForAccount(It.IsAny<string>()), Times.Exactly(4));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("has started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("finished successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task GenerateFeedbackTransactionsTimer_WhenGetFeedbackTransactionAccountIdsThrows_RetriesAndSucceeds()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);
            var accountIds = new List<string> { "account1" };
            var response = new GetFeedbackTransactionAccountIdsResponse { AccountIds = accountIds };

            _apiMock.SetupSequence(x => x.GetFeedbackTransactionAccountIds(It.IsAny<int>()))
                .ThrowsAsync(new HttpRequestException("Temporary error"))
                .ReturnsAsync(response);

            await _function.GenerateFeedbackTransactionsTimer(timerInfo);

            _apiMock.Verify(x => x.GetFeedbackTransactionAccountIds(It.IsAny<int>()), Times.Exactly(2));
            _apiMock.Verify(x => x.ProcessFeedbackTransactionForAccount("account1"), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retrying operation") && v.ToString().Contains("attempt 2 of 3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task GenerateFeedbackTransactionsTimer_WhenProcessingAccountFails_ContinuesWithOtherAccounts()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);
            var accountIds = new List<string> { "account1", "account2", "account3" };
            var response = new GetFeedbackTransactionAccountIdsResponse { AccountIds = accountIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionAccountIds(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.ProcessFeedbackTransactionForAccount("account2"))
                .ThrowsAsync(new Exception("Processing failed"));

            await _function.GenerateFeedbackTransactionsTimer(timerInfo);

            _apiMock.Verify(x => x.ProcessFeedbackTransactionForAccount("account1"), Times.Once);
            _apiMock.Verify(x => x.ProcessFeedbackTransactionForAccount("account2"), Times.Exactly(MaxRetryAttempts));
            _apiMock.Verify(x => x.ProcessFeedbackTransactionForAccount("account3"), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("finished successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to process feedback transaction for account account2 after 3 attempts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void GenerateFeedbackTransactionsTimer_WhenGetFeedbackTransactionAccountIdsFails_ThrowsException()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);
            var exception = new Exception("API failed");

            _apiMock.Setup(x => x.GetFeedbackTransactionAccountIds(It.IsAny<int>())).ThrowsAsync(exception);

            var ex = Assert.ThrowsAsync<Exception>(() => _function.GenerateFeedbackTransactionsTimer(timerInfo));
            Assert.That(ex, Is.EqualTo(exception));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("has failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task GenerateFeedbackTransactionsTimer_RespectsMaxParallelismSetting()
        {
            _configuration.GenerateFeedbackTransactionsMaxParallelism = 1;
            _function = new GenerateFeedbackTransactionsFunction(_loggerMock.Object, _apiMock.Object, _configuration);

            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);
            var accountIds = new List<string> { "account1", "account2" };
            var response = new GetFeedbackTransactionAccountIdsResponse { AccountIds = accountIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionAccountIds(It.IsAny<int>())).ReturnsAsync(response);

            await _function.GenerateFeedbackTransactionsTimer(timerInfo);

            _apiMock.Verify(x => x.ProcessFeedbackTransactionForAccount(It.IsAny<string>()), Times.Exactly(2));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processing 2 accounts with max parallelism of 1")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public async Task GenerateFeedbackTransactionsExecuteWithRetry_RetriesCorrectNumberOfTimes()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);
            var accountIds = new List<string> { "account1" };
            var response = new GetFeedbackTransactionAccountIdsResponse { AccountIds = accountIds };

            _apiMock.Setup(x => x.GetFeedbackTransactionAccountIds(It.IsAny<int>())).ReturnsAsync(response);
            _apiMock.Setup(x => x.ProcessFeedbackTransactionForAccount("account1"))
                .ThrowsAsync(new HttpRequestException("Temporary failure"));

            await _function.GenerateFeedbackTransactionsTimer(timerInfo);

            _apiMock.Verify(x => x.ProcessFeedbackTransactionForAccount("account1"), Times.Exactly(MaxRetryAttempts));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retrying operation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Exactly(MaxRetryAttempts - 1));
        }
    }
}