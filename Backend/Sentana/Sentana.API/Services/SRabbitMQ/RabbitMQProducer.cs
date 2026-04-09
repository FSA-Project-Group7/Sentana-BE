using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Sentana.API.Services.SRabbitMQ
{
    public class RabbitMQProducer : IRabbitMQProducer
    {
        private const string EmailQueueName = "email_queue";
        private const string NotificationQueueName = "notification_queue";

        private readonly IRabbitMQConnection _connection;

        public RabbitMQProducer(IRabbitMQConnection connection)
        {
            _connection = connection;
        }

        public Task SendEmailMessage<T>(T message) => PublishAsync(EmailQueueName, message);

        public Task SendNotificationMessage<T>(T message) => PublishAsync(NotificationQueueName, message);

        private async Task PublishAsync<T>(string queueName, T message)
        {
            var connection = await _connection.GetConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);
            var properties = new BasicProperties
            {
                Persistent = true
            };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: body);
        }
    }
}
