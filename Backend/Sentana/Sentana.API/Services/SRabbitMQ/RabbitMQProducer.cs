using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Sentana.API.Services.SRabbitMQ
{
    public class RabbitMQProducer : IRabbitMQProducer
    {
        public async Task SendEmailMessage<T>(T message)
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: "email_queue",
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
                routingKey: "email_queue",
                mandatory: false,
                basicProperties: properties,
                body: body);
        }
    }
}
