using LandaDoc.Identity.Data;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Consumers;

public class DoctorSuspendedConsumer(IdentityDbContext db) : IConsumer<DoctorSuspendedEvent>
{
    public async Task Consume(ConsumeContext<DoctorSuspendedEvent> ctx)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == ctx.Message.UserId);
        if (user is null) return;

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
