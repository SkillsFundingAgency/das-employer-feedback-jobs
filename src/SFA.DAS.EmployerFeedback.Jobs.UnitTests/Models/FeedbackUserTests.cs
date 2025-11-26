using NUnit.Framework;
using SFA.DAS.EmployerFeedback.Infrastructure.Models;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Models
{
    [TestFixture]
    public class FeedbackUserTests
    {
        [Test]
        public void Properties_ShouldAllowGetAndSet()
        {
            var feedbackUser = new FeedbackUser();
            var expectedName = "John Doe";
            var expectedEmail = "john.doe@example.com";

            feedbackUser.Name = expectedName;
            feedbackUser.Email = expectedEmail;

            Assert.That(feedbackUser.Name, Is.EqualTo(expectedName));
            Assert.That(feedbackUser.Email, Is.EqualTo(expectedEmail));
        }
    }
}
