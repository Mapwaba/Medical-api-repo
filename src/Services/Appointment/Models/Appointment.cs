using LandaDoc.Shared.Models;

namespace LandaDoc.Appointment.Models;

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int RefNumber { get; set; }
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public Guid BookedByUserId { get; set; } // who actually made the booking — self, or an authorized family member
    public Guid? ClinicId { get; set; }
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public string? Motif { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
