using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sentana.API.DTOs.Email;
using Sentana.API.Services.SEmail;
using Sentana.API.Services.SRabbitMQ;
using Microsoft.Extensions.Options;

namespace Sentana.API.BackgroundServices
{
    public class EmailConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMQOptions _rabbitOptions;
        private IConnection? _connection;
        private IChannel? _channel;

        public EmailConsumerService(IServiceProvider serviceProvider, IOptions<RabbitMQOptions> rabbitOptions)
        {
            _serviceProvider = serviceProvider;
            _rabbitOptions = rabbitOptions.Value;
        }
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitOptions.HostName,
                Port = _rabbitOptions.Port,
                VirtualHost = _rabbitOptions.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true
            };
            if (!string.IsNullOrWhiteSpace(_rabbitOptions.UserName)) factory.UserName = _rabbitOptions.UserName;
            if (!string.IsNullOrWhiteSpace(_rabbitOptions.Password)) factory.Password = _rabbitOptions.Password;

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync();

            await _channel.QueueDeclareAsync(
                queue: "email_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Giới hạn số message đang xử lý đồng thời (backpressure)
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 20, global: false);

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null) return;
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var emailData = JsonSerializer.Deserialize<EmailMessageDto>(message);

                if (emailData != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                    try
                    {
                        await emailSender.SendAsync(emailData.To, emailData.Subject, emailData.Body, stoppingToken);
                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                    catch (Exception)
                    {
                        await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                    }
                }
            };

            await _channel.BasicConsumeAsync(queue: "email_queue", autoAck: false, consumer: consumer);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null) await _channel.CloseAsync();
            if (_connection != null) await _connection.CloseAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}
