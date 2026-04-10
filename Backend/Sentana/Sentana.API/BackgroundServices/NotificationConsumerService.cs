using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sentana.API.Constants;
using Sentana.API.DTOs.Notification;
using Sentana.API.Hubs;
using Sentana.API.Models;
using Sentana.API.Services.SRabbitMQ;

namespace Sentana.API.BackgroundServices
{
    public class NotificationConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<NotificationConsumerService> _logger;
        private readonly RabbitMQOptions _rabbitOptions;

        private IConnection? _connection;
        private IChannel? _channel;

        private readonly List<(ulong DeliveryTag, NotificationMessageDto Msg)> _buffer = new();
        private readonly SemaphoreSlim _flushGate = new(1, 1);

        private const string QueueName = "notification_queue";
        private const int BatchSize = 100;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(500);

        public NotificationConsumerService(
            IServiceProvider serviceProvider,
            IHubContext<NotificationHub> hubContext,
            ILogger<NotificationConsumerService> logger,
            IOptions<RabbitMQOptions> rabbitOptions)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
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
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Backpressure: chỉ "đứng tên" tối đa N message chưa ack
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 500, global: false);

            await base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null) return;

            _ = Task.Run(() => FlushLoopAsync(stoppingToken), stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var msg = JsonSerializer.Deserialize<NotificationMessageDto>(json);

                    if (msg == null || msg.AccountId <= 0) { await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken); return; }

                    bool shouldFlush = false;
                    lock (_buffer)
                    {
                        _buffer.Add((ea.DeliveryTag, msg));
                        shouldFlush = _buffer.Count >= BatchSize;
                    }

                    if (shouldFlush)
                    {
                        await FlushAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle notification message.");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        }

        private async Task FlushLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(FlushInterval, stoppingToken);
                    await FlushAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Notification flush loop error.");
                }
            }
        }

        private async Task FlushAsync(CancellationToken stoppingToken)
        {
            if (_channel == null) return;

            await _flushGate.WaitAsync(stoppingToken);
            List<(ulong DeliveryTag, NotificationMessageDto Msg)>? batch = null;
            try
            {
                lock (_buffer)
                {
                    if (_buffer.Count == 0) return;
                    batch = _buffer.ToList();
                    _buffer.Clear();
                }

                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SentanaContext>();

                var entities = batch.Select(x => new Notification
                {
                    AccountId = x.Msg.AccountId,
                    Title = x.Msg.Title,
                    Message = x.Msg.Message,
                    IsRead = false,
                    CreatedAt = x.Msg.CreatedAt
                }).ToList();

                db.Notifications.AddRange(entities);
                await db.SaveChangesAsync(stoppingToken);

                // Group theo user để giảm số call SignalR
                var groups = entities
                    .GroupBy(n => n.AccountId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(n => new
                        {
                            n.NotificationId,
                            n.Title,
                            n.Message,
                            n.CreatedAt
                        }).ToList());

                foreach (var kvp in groups)
                {
                    await _hubContext.Clients.User(kvp.Key.ToString()).SendAsync(
                        SignalREvents.NOTIFICATION_RECEIVED,
                        kvp.Value,
                        stoppingToken);
                }

                foreach (var item in batch)
                {
                    await _channel.BasicAckAsync(item.DeliveryTag, false, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush notification batch.");
                if (batch != null && _channel != null)
                {
                    foreach (var item in batch)
                    {
                        try
                        {
                            await _channel.BasicNackAsync(item.DeliveryTag, false, true, stoppingToken);
                        }
                        catch
                        {
                            // ignore individual nack failures
                        }
                    }
                }
            }
            finally
            {
                _flushGate.Release();
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await FlushAsync(cancellationToken);
            }
            catch
            {
                // ignore on shutdown
            }

            if (_channel != null) await _channel.CloseAsync(cancellationToken);
            if (_connection != null) await _connection.CloseAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}

