using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using SFA.DAS.EmployerFeedback.Jobs.Services;

namespace SFA.DAS.EmployerFeedback.Jobs.UnitTests.Services
{
    public record Input(int Id);
    public record Output(int Id, string Status);

    [TestFixture]
    public class WaveFanoutServiceTests
    {
        private const int InterwaveWaitMs = 5000;
        private Mock<ILogger<WaveFanoutService>> _loggerMock;
        private WaveFanoutService _service;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<WaveFanoutService>>();
            _service = new WaveFanoutService(_loggerMock.Object);
        }

        private static Task<Output> DummyStart(Input input)
            => Task.FromResult(new Output(input.Id, "OK"));

        [Test]
        public async Task ExecuteAsync_UnderCap_SendsAll_InSingleWave_NoWaits()
        {
            var items = Enumerable.Range(1, 10).Select(i => new Input(i)).ToList();
            var startTime = DateTime.UtcNow;

            var results = await _service.ExecuteAsync(
                items,
                DummyStart,
                perSecondCap: 55,
                delayBetweenWavesMs: InterwaveWaitMs);

            var endTime = DateTime.UtcNow;

            Assert.That(results, Has.Count.EqualTo(10));
            Assert.That(results.Select(r => r.Id), Is.EquivalentTo(items.Select(x => x.Id)));

            Assert.That(endTime - startTime, Is.LessThan(TimeSpan.FromMilliseconds(InterwaveWaitMs / 2)));
        }

        [Test]
        public async Task ExecuteAsync_OverCap_TwoWaves_WaitsBetweenWaves()
        {
            var items = Enumerable.Range(1, 60).Select(i => new Input(i)).ToList();
            var startTime = DateTime.UtcNow;

            var results = await _service.ExecuteAsync(
                items,
                DummyStart,
                perSecondCap: 55,
                delayBetweenWavesMs: InterwaveWaitMs);

            var endTime = DateTime.UtcNow;

            Assert.That(results, Has.Count.EqualTo(60));
            Assert.That(results.Select(r => r.Id), Is.EquivalentTo(items.Select(x => x.Id)));

            Assert.That(endTime - startTime, Is.GreaterThan(TimeSpan.FromMilliseconds(InterwaveWaitMs * 0.8)));
        }

        [Test]
        public async Task ExecuteAsync_ExactlyAtCap_SingleWave_NoWaits()
        {
            var items = Enumerable.Range(1, 55).Select(i => new Input(i)).ToList();
            var startTime = DateTime.UtcNow;

            var results = await _service.ExecuteAsync(
                items,
                DummyStart,
                perSecondCap: 55,
                delayBetweenWavesMs: InterwaveWaitMs);

            var endTime = DateTime.UtcNow;

            Assert.That(results, Has.Count.EqualTo(55));
            Assert.That(results.Select(r => r.Id), Is.EquivalentTo(items.Select(x => x.Id)));

            Assert.That(endTime - startTime, Is.LessThan(TimeSpan.FromMilliseconds(InterwaveWaitMs / 2)));
        }

        [Test]
        public async Task ExecuteAsync_WaitsInterwave_AfterSlowestTaskInWave()
        {
            var items = Enumerable.Range(1, 6).Select(i => new Input(i)).ToList();
            var startTime = DateTime.UtcNow;

            Task<Output> SlowStart(Input input)
            {
                var delay = input.Id <= 3 ? input.Id * 100 : 0;
                return Task.Delay(delay).ContinueWith(_ => new Output(input.Id, "OK"));
            }

            var results = await _service.ExecuteAsync(
                items,
                SlowStart,
                perSecondCap: 3,
                delayBetweenWavesMs: InterwaveWaitMs);

            var endTime = DateTime.UtcNow;

            Assert.That(results, Has.Count.EqualTo(6));
            Assert.That(results.Select(r => r.Id), Is.EquivalentTo(items.Select(x => x.Id)));

            var expectedMinTime = TimeSpan.FromMilliseconds(300 + InterwaveWaitMs * 0.8);
            Assert.That(endTime - startTime, Is.GreaterThan(expectedMinTime));
        }

        [Test]
        public async Task ExecuteAsync_LargeBatch_SchedulesInterwaveWaitBetweenEachWave()
        {
            var items = Enumerable.Range(1, 170).Select(i => new Input(i)).ToList();
            var startTime = DateTime.UtcNow;

            var results = await _service.ExecuteAsync(
                items,
                DummyStart,
                perSecondCap: 55,
                delayBetweenWavesMs: InterwaveWaitMs);

            var endTime = DateTime.UtcNow;

            Assert.That(results, Has.Count.EqualTo(170));
            Assert.That(results.Select(r => r.Id), Is.EquivalentTo(items.Select(x => x.Id)));

            var expectedMinTime = TimeSpan.FromMilliseconds(3 * InterwaveWaitMs * 0.8);
            Assert.That(endTime - startTime, Is.GreaterThan(expectedMinTime));
        }
    }
}
