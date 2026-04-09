namespace Sentana.API.Services.SEmail
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
    }
}

