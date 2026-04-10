using Sentana.API.DTOs.Notification;
using Sentana.API.Services.SRabbitMQ;

namespace Sentana.API.Services.SNotification
{
    public class NotificationPublisher : INotificationPublisher
    {
        private readonly IRabbitMQProducer _producer;
        public NotificationPublisher(IRabbitMQProducer producer) { _producer = producer; }

        public Task QueueNotificationAsync(int accountId, string title, string message, CancellationToken cancellationToken = default)
        {
            var dto = new NotificationMessageDto
            {
                AccountId = accountId,
                Title = title,
                Message = message,
                CreatedAt = DateTime.Now
            };

            return _producer.SendNotificationMessage(dto);
        }
    }
}

