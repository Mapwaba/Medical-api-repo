namespace LandaDoc.Shared.DTOs;

public record DocumentDto(
    Guid Id, Guid PatientId, Guid? DoctorId, Guid? AppointmentId,
    string FileName, string ContentType, long SizeBytes, string Category,
    DateTime CreatedAt
);
