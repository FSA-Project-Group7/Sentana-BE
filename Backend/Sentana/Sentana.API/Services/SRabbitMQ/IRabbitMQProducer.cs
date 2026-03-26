namespace Sentana.API.Services.SRabbitMQ
{
    public interface IRabbitMQProducer
    {
        Task SendEmailMessage<T>(T message);    
    }
}
