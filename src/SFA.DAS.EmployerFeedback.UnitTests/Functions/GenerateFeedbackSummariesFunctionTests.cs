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
    public class GenerateFeedbackSummariesFunctionTests
    {
        private Mock<ILogger<GenerateFeedbackSummariesFunction>> _loggerMock;
        private Mock<IEmployerFeedbackOuterApi> _apiMock;
        private GenerateFeedbackSummariesFunction _function;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<GenerateFeedbackSummariesFunction>>();
            _apiMock = new Mock<IEmployerFeedbackOuterApi>();
            _function = new GenerateFeedbackSummariesFunction(_loggerMock.Object, _apiMock.Object);
        }

        [Test]
        public async Task GenerateFeedbackSummariesFunctionTimer_CallsApiAndLogsInformation()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);

            await _function.GenerateFeedbackSummariesFunctionTimer(timerInfo);

            _apiMock.Verify(api => api.GenerateFeedbackSummaries(), Times.Once);
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
        public void GenerateFeedbackSummariesFunctionTimer_WhenApiThrows_LogsErrorAndThrows()
        {
            var timerInfo = (Microsoft.Azure.Functions.Worker.TimerInfo)Activator.CreateInstance(typeof(Microsoft.Azure.Functions.Worker.TimerInfo), true);
            var exception = new Exception("API failed");
            _apiMock.Setup(api => api.GenerateFeedbackSummaries()).ThrowsAsync(exception);

            var ex = Assert.ThrowsAsync<Exception>(() => _function.GenerateFeedbackSummariesFunctionTimer(timerInfo));
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
