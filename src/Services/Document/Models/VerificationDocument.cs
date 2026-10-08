namespace LandaDoc.Document.Models;

// A document a doctor sends so admins can verify who they are before approving them: an ID card
// or passport, their medical-council membership card, or their contract with the establishment
// they work for (VerificationDocumentKinds). Only the doctor and admins can see these.
public class VerificationDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }             // the doctor's account
    public string Kind { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = "";  // S3/R2 object key, under verification/{userId}/
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
