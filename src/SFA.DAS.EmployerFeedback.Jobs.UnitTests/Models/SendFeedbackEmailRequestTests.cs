using System;
using NUnit.Framework;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Models
{
    [TestFixture]
    public class SendFeedbackEmailRequestTests
    {
        [Test]
        public void Properties_ShouldAllowGetAndSet()
        {
            var request = new SendFeedbackEmailRequest();
            var expectedTemplateId = Guid.NewGuid();
            var expectedEmail = "test@example.com";
            var expectedContact = "John Doe";
            var expectedEmployerName = "Test Employer Ltd";
            var expectedAccountHashedId = "ABC123";
            var expectedAccountsBaseUrl = "https://accounts.example.com";
            var expectedFeedbackBaseUrl = "https://feedback.example.com";

            request.TemplateId = expectedTemplateId;
            request.Email = expectedEmail;
            request.Contact = expectedContact;
            request.EmployerName = expectedEmployerName;
            request.AccountHashedId = expectedAccountHashedId;
            request.AccountsBaseUrl = expectedAccountsBaseUrl;
            request.FeedbackBaseUrl = expectedFeedbackBaseUrl;

            Assert.That(request.TemplateId, Is.EqualTo(expectedTemplateId));
            Assert.That(request.Email, Is.EqualTo(expectedEmail));
            Assert.That(request.Contact, Is.EqualTo(expectedContact));
            Assert.That(request.EmployerName, Is.EqualTo(expectedEmployerName));
            Assert.That(request.AccountHashedId, Is.EqualTo(expectedAccountHashedId));
            Assert.That(request.AccountsBaseUrl, Is.EqualTo(expectedAccountsBaseUrl));
            Assert.That(request.FeedbackBaseUrl, Is.EqualTo(expectedFeedbackBaseUrl));
        }
    }
}