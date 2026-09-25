namespace LandaDoc.Document.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Category { get; set; } = "Other"; // LabResult | Prescription | Report | Other
    public string StorageKey { get; set; } = ""; // S3/MinIO object key
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
