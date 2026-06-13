namespace NzbDrone.Core.Jobs.Durable
{
    /// <summary>
    /// Allows long-running job handlers to report incremental progress (0-100)
    /// back to the durable job record.
    /// </summary>
    public interface IJobProgressReporter
    {
        /// <summary>
        /// Report progress for the currently executing durable job attempt.
        /// If no durable context is active the call is a no-op.
        /// </summary>
        void ReportProgress(int percent);
    }
}
