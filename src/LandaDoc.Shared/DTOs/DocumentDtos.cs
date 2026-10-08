namespace LandaDoc.Shared.DTOs;

public record DocumentDto(
    Guid Id, Guid PatientId, Guid? DoctorId, Guid? AppointmentId,
    string FileName, string ContentType, long SizeBytes, string Category,
    DateTime CreatedAt
);

// Documents a doctor sends for verification (ID, membership card, establishment contract)
public record VerificationDocumentDto(
    Guid Id, Guid UserId, string Kind, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);

public static class VerificationDocumentKinds
{
    public const string IdDocument = "IdDocument";                       // ID card or passport
    public const string MembershipCard = "MembershipCard";               // medical-council membership card
    public const string EstablishmentContract = "EstablishmentContract"; // contract with the establishment

    public static readonly string[] All = [IdDocument, MembershipCard, EstablishmentContract];
    // A doctor needs these before applying; the contract is optional
    public static readonly string[] Required = [IdDocument, MembershipCard];
}
