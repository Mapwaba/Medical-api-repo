namespace LandaDoc.Payment.Models;

// Which partner insurers a doctor takes. No row = every active insurer (the default, so
// doctors who never chose keep accepting insurance); a row limits it to InsurerIds, and an
// empty list means the doctor doesn't take insurance at all.
public class DoctorInsurerChoice
{
    public Guid DoctorId { get; set; }
    public List<Guid> InsurerIds { get; set; } = [];
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
