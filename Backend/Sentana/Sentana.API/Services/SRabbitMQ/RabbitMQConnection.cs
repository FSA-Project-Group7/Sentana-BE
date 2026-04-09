using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Sentana.API.Services.SRabbitMQ
{
    public sealed class RabbitMQConnection : IRabbitMQConnection
    {
        private readonly RabbitMQOptions _options;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private IConnection? _connection;

        public RabbitMQConnection(IOptions<RabbitMQOptions> options)
        {
            _options = options.Value;
        }

        public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_connection is { IsOpen: true }) return _connection;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_connection is { IsOpen: true }) return _connection;

                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    VirtualHost = _options.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true,
                };

                if (!string.IsNullOrWhiteSpace(_options.UserName))
                {
                    factory.UserName = _options.UserName;
                }

                if (!string.IsNullOrWhiteSpace(_options.Password))
                {
                    factory.Password = _options.Password;
                }

                _connection = await factory.CreateConnectionAsync(cancellationToken);
                return _connection;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (_connection != null)
                {
                    await _connection.CloseAsync();
                    _connection.Dispose();
                    _connection = null;
                }
            }
            finally
            {
                _gate.Release();
                _gate.Dispose();
            }
        }
    }
}

