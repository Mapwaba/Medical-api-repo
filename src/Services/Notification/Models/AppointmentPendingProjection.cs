using LandaDoc.Shared.Models;

namespace LandaDoc.Notification.Models;

// Local read-model projection of appointment status, built from BookingCreated/PaymentCompleted/
// PaymentFailed/AppointmentCompleted events. The pending count shown in the badge is always
// recomputed from this table (never incremented/decremented in place), so it stays correct even
// if MassTransit redelivers an event.
public class AppointmentPendingProjection
{
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public AppointmentStatus Status { get; set; }
    // The family member who booked it for the patient (null when the patient booked it, or for
    // bookings made before this was recorded) and the patient's name, for the booker's messages
    public Guid? BookedByUserId { get; set; }
    public string? PatientName { get; set; }
}
