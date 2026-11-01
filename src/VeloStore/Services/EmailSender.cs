using Microsoft.AspNetCore.Identity.UI.Services;

namespace VeloStore.Services
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Fake sender (dev only)
            return Task.CompletedTask;
        }
    }
}
