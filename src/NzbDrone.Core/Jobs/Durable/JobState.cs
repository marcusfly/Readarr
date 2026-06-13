namespace NzbDrone.Core.Jobs.Durable
{
    public enum JobState
    {
        Queued = 0,
        Running = 1,
        Retrying = 2,
        Completed = 3,
        Failed = 4,
        Canceled = 5
    }
}
