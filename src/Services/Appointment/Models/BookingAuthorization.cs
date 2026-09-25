namespace LandaDoc.Appointment.Models;

// Local read-model, projected from Identity's family-link/dependent events.
// Never written to directly from client input — only from the consumers in
// Consumers/, which react to Identity-originated events.
public class BookingAuthorization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookerId { get; set; }        // who is allowed to book
    public Guid TargetId { get; set; }        // for whom (a User.Id or a Dependent.Id)
    public bool IsDependentTarget { get; set; }
    public Guid SourceId { get; set; }        // Identity's FamilyLink.Id or Dependent.Id — for cleanup on revoke/remove
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
