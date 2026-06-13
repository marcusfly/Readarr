namespace NzbDrone.Core.Jobs.Durable
{
    /// <summary>
    /// No-op implementation used when no durable job context is active.
    /// The DI container registers DurableJobScheduler as the primary
    /// IJobProgressReporter; this class serves as a fallback for callers
    /// that resolve the interface without going through the scheduler.
    /// </summary>
    public class NullJobProgressReporter : IJobProgressReporter
    {
        public void ReportProgress(int percent)
        {
            // Intentionally empty.
        }
    }
}
