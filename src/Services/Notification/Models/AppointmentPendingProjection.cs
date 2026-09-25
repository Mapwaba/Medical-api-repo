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
}
