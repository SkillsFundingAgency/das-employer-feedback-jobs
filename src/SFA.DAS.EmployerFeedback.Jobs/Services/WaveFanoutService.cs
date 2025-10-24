using Microsoft.Extensions.Logging;

namespace SFA.DAS.EmployerFeedback.Jobs.Services
{
    public class WaveFanoutService : IWaveFanoutService
    {
        private readonly ILogger<WaveFanoutService> _logger;

        public WaveFanoutService(ILogger<WaveFanoutService> logger)
        {
            _logger = logger;
        }

        public async Task<IReadOnlyList<TOut>> ExecuteAsync<TIn, TOut>(
            IEnumerable<TIn> items,
            Func<TIn, Task<TOut>> startFunc,
            int perSecondCap = 10,
            int delayBetweenWavesMs = 1000)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(startFunc);

            if (perSecondCap <= 0) throw new ArgumentOutOfRangeException(nameof(perSecondCap));

            var list = (items as IList<TIn>) ?? items.ToList();
            var results = new List<TOut>(list.Count);

            _logger.LogDebug("WaveFanOut: Activities to process {ActivityCount}", list.Count);

            int index = 0;
            while (index < list.Count)
            {
                int remaining = list.Count - index;
                int take = Math.Min(perSecondCap, remaining);

                var waveTasks = new List<Task<TOut>>(take);
                for (int k = 0; k < take; k++)
                {
                    waveTasks.Add(startFunc(list[index + k]));
                }

                _logger.LogDebug("WaveFanOut: Activities tasks to wait for {TaskCount}", waveTasks.Count);

                var waveResults = await Task.WhenAll(waveTasks);
                results.AddRange(waveResults);
                index += take;

                if (index < list.Count)
                {
                    _logger.LogDebug("WaveFanOut: Waiting {DelayMs}ms before next wave", delayBetweenWavesMs);
                    await Task.Delay(delayBetweenWavesMs);
                    _logger.LogDebug("WaveFanOut: Resumed");
                }
            }

            _logger.LogDebug("WaveFanOut: Results to report {ResultCount}", results.Count);

            return results;
        }
    }
}