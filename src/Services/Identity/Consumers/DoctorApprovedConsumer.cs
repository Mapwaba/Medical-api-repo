using LandaDoc.Identity.Data;
using LandaDoc.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Consumers;

public class DoctorApprovedConsumer(IdentityDbContext db) : IConsumer<DoctorApprovedEvent>
{
    public async Task Consume(ConsumeContext<DoctorApprovedEvent> ctx)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == ctx.Message.UserId);
        if (user is null) return;

        user.IsApproved = true;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
