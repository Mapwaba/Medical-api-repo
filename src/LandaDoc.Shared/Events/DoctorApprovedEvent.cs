using LandaDoc.Shared.DTOs;

namespace LandaDoc.Shared.Events;

// Published by the Admin service when a doctor profile is approved (or reinstated
// after a suspension). The Identity service consumes this to flip User.IsApproved;
// the Search service consumes it to build/refresh the doctor search projection.
public record DoctorApprovedEvent(
    Guid DoctorProfileId,
    Guid UserId,
    string FirstName,
    string LastName,
    string Specialty,
    string? Bio,
    decimal ConsultationFee,
    List<ClinicDto> Clinics,
    DateTime OccurredAt
);
