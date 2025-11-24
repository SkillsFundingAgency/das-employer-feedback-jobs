using System;
using NUnit.Framework;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Models
{
    [TestFixture]
    public class UpdateFeedbackTransactionRequestTests
    {
        [Test]
        public void Properties_ShouldAllowGetAndSet()
        {
            var request = new UpdateFeedbackTransactionRequest();
            var expectedTemplateId = Guid.NewGuid();
            var expectedSentCount = 25;
            var expectedSentDate = new DateTime(2024, 12, 15, 14, 30, 0);

            request.TemplateId = expectedTemplateId;
            request.SentCount = expectedSentCount;
            request.SentDate = expectedSentDate;

            Assert.That(request.TemplateId, Is.EqualTo(expectedTemplateId));
            Assert.That(request.SentCount, Is.EqualTo(expectedSentCount));
            Assert.That(request.SentDate, Is.EqualTo(expectedSentDate));
        }


    }
}