namespace Sentana.API.Services.SNotification
{
    public interface INotificationPublisher
    {
        Task QueueNotificationAsync(int accountId, string title, string message, CancellationToken cancellationToken = default);
    }
}

