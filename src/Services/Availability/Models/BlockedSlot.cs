namespace LandaDoc.Availability.Models;

// A one-off slot a doctor has blocked out (vacation, admin time, etc.),
// on top of their recurring Schedule.
public class BlockedSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public DateTime SlotStart { get; set; }
    public string? Reason { get; set; }
}
