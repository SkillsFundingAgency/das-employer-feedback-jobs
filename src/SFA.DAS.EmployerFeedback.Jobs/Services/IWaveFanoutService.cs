namespace SFA.DAS.EmployerFeedback.Jobs.Services
{
    public interface IWaveFanoutService
    {
        Task<IReadOnlyList<TOut>> ExecuteAsync<TIn, TOut>(
            IEnumerable<TIn> items,
            Func<TIn, Task<TOut>> startFunc,
            int perSecondCap = 1,
            int delayBetweenWavesMs = 1000);
    }
}