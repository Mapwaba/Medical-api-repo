using LandaDoc.Shared.Models;

namespace LandaDoc.Availability.Models;

// Read-only projection of the `appointments` table, which the Appointment
// service owns and migrates. Only the columns Availability needs are mapped.
public class AppointmentSlot
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime SlotStart { get; set; }
    public AppointmentStatus Status { get; set; }
}
