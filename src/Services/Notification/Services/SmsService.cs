using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace LandaDoc.Notification.Services;

public class SmsService(IConfiguration cfg) : ISmsService
{
    public async Task SendAsync(string to, string body)
    {
        TwilioClient.Init(cfg["Twilio:AccountSid"], cfg["Twilio:AuthToken"]);
        await MessageResource.CreateAsync(
            body: body,
            from: new PhoneNumber(cfg["Twilio:From"]),
            to: new PhoneNumber(to));
    }
}
