using SendGrid;
using SendGrid.Helpers.Mail;

namespace LandaDoc.Notification.Services;

public class EmailService(IConfiguration cfg) : IEmailService
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var client = new SendGridClient(cfg["SendGrid:ApiKey"]);
        var from = new EmailAddress(cfg["SendGrid:From"], "LandaDoc");
        var msg = MailHelper.CreateSingleEmail(from, new EmailAddress(to), subject, body, body);
        await client.SendEmailAsync(msg);
    }
}
