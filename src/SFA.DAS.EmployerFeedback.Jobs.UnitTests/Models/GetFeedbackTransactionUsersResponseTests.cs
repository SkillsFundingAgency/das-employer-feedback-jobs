using System.Collections.Generic;
using NUnit.Framework;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Models
{
    [TestFixture]
    public class GetFeedbackTransactionUsersResponseTests
    {
        [Test]
        public void Properties_ShouldAllowGetAndSet()
        {
            var response = new GetFeedbackTransactionUsersResponse();
            var expectedAccountId = 12345L;
            var expectedAccountName = "Test Account";
            var expectedTemplateName = "Test Template";
            var expectedUsers = new List<FeedbackUser>
            {
                new FeedbackUser { Name = "John Doe", Email = "john.doe@example.com" },
                new FeedbackUser { Name = "Jane Smith", Email = "jane.smith@example.com" }
            };

            response.AccountId = expectedAccountId;
            response.AccountName = expectedAccountName;
            response.TemplateName = expectedTemplateName;
            response.Users = expectedUsers;

            Assert.That(response.AccountId, Is.EqualTo(expectedAccountId));
            Assert.That(response.AccountName, Is.EqualTo(expectedAccountName));
            Assert.That(response.TemplateName, Is.EqualTo(expectedTemplateName));
            Assert.That(response.Users, Is.SameAs(expectedUsers));
            Assert.That(response.Users, Has.Count.EqualTo(2));
        }
    }
}