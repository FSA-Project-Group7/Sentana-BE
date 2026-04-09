using RabbitMQ.Client;

namespace Sentana.API.Services.SRabbitMQ
{
    public interface IRabbitMQConnection : IAsyncDisposable
    {
        Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
    }
}

