using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmployerFeedback.Infrastructure.Api;
using SFA.DAS.EmployerFeedback.Jobs.Functions;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Functions
{
    [TestFixture]
    public class SyncEmployerAccountsFunctionTests
    {
        private Mock<ILogger<SyncEmployerAccountsFunction>> _loggerMock;
        private Mock<IEmployerFeedbackOuterApi> _apiMock;
        private SyncEmployerAccountsFunction _function;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<SyncEmployerAccountsFunction>>();
            _apiMock = new Mock<IEmployerFeedbackOuterApi>();
            _function = new SyncEmployerAccountsFunction(_loggerMock.Object, _apiMock.Object);
        }

        [Test]
        public async Task SyncEmployerAccountsTimer_CallsApiAndLogsInformation()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);

            await _function.SyncEmployerAccountsTimer(timerInfo);

            _apiMock.Verify(api => api.SyncEmployerAccounts(), Times.Once);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("has started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("has finished")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Test]
        public void SyncEmployerAccountsTimer_WhenApiThrows_LogsErrorAndThrows()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);
            var exception = new Exception("API failed");
            _apiMock.Setup(api => api.SyncEmployerAccounts()).ThrowsAsync(exception);

            var ex = Assert.ThrowsAsync<Exception>(() => _function.SyncEmployerAccountsTimer(timerInfo));
            Assert.That(ex, Is.EqualTo(exception));

            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("has failed")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }
    }
}
