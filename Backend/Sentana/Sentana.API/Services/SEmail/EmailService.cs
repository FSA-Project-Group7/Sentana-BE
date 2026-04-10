using Sentana.API.DTOs.Email;
using Sentana.API.Services.SEmail;
using Sentana.API.Services.SRabbitMQ;

namespace Sentana.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly IRabbitMQProducer _producer;
        public EmailService(IRabbitMQProducer producer) { _producer = producer; }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var msg = new EmailMessageDto
            {
                To = toEmail,
                Subject = subject,
                Body = body
            };

            await _producer.SendEmailMessage(msg);
        }
    }
}