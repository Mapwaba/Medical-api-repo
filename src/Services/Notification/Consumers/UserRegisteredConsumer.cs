using LandaDoc.Notification.Data;
using LandaDoc.Notification.Models;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Notification.Consumers;

// Builds Notification's local read-model of "who is this user, how do we reach them" —
// needed to send payment receipts/failure notices without calling Identity synchronously.
public class UserRegisteredConsumer(NotificationDbContext db) : IConsumer<UserRegisteredEvent>
{
    public async Task Consume(ConsumeContext<UserRegisteredEvent> ctx)
    {
        var msg = ctx.Message;
        var contact = await db.UserContacts.FirstOrDefaultAsync(c => c.UserId == msg.UserId);
        if (contact is null)
        {
            db.UserContacts.Add(new UserContact
            {
                UserId = msg.UserId,
                Email = msg.Email,
                Phone = msg.Phone,
                FirstName = msg.FirstName,
                LastName = msg.LastName
            });
        }
        else
        {
            contact.Email = msg.Email;
            contact.Phone = msg.Phone;
            contact.FirstName = msg.FirstName;
            contact.LastName = msg.LastName;
        }
        await db.SaveChangesAsync();
    }
}
