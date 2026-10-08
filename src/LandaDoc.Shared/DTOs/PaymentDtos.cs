using System.ComponentModel.DataAnnotations;
using LandaDoc.Shared.Models;

namespace LandaDoc.Shared.DTOs;

public record PaymentDto(
    Guid Id, Guid AppointmentId, Guid DoctorId, Guid PatientId,
    decimal GrossAmount, decimal PlatformFee, decimal NetAmount,
    PaymentStatus Status, PaymentProvider? Provider, string? ProviderRef, DateTime CreatedAt
);

// PatientFullName is required only for MokoAfrika — its debit request wants a first/last
// name for the mobile money receipt, and Payment has no local read-model of patient profile
// data (unlike DoctorFee, nothing publishes it). Simplest source is the patient typing it at
// checkout, same as any mobile money form. Email comes from the caller's own JWT claim instead.
// InsurerId/MemberNumber/MemberName are for PaymentProvider.Insurance only (MemberName is the
// main member when the patient is covered as a dependant). Optional so older clients still bind.
public record InitiatePaymentRequest(
    PaymentProvider Provider, string? PhoneNumber, MobileMoneyOperator? Operator, string? PatientFullName,
    Guid? InsurerId = null, string? MemberNumber = null, string? MemberName = null);

public record InitiatePaymentResponse(PaymentProvider Provider, string? CheckoutUrl, string? Message);

// ── Insurance / medical aid partners ────────────────────────────────

public record InsurerDto(Guid Id, string Name, string? Phone, string? Email, bool IsActive);

public record SaveInsurerRequest([Required, StringLength(120)] string Name, string? Phone, string? Email, bool IsActive = true);

public record InsuranceClaimDto(
    Guid Id, Guid PaymentId, Guid AppointmentId, Guid DoctorId, Guid PatientId,
    Guid InsurerId, string InsurerName, string MemberNumber, string? MemberName,
    decimal Amount, InsuranceClaimStatus Status,
    string? Note, string? InsurerReference,
    DateTime CreatedAt, DateTime UpdatedAt,
    // What the insurer gave the doctor when they confirmed the cover (authorisation number,
    // or the name of the agent they spoke to); set when the claim is approved
    string? AuthorizationReference = null
);

// The insurers a doctor takes. AcceptsAll = every active partner, including ones added later
// (the default for a doctor who never chose); otherwise only InsurerIds (empty = none).
public record AcceptedInsurersDto(bool AcceptsAll, List<Guid> InsurerIds);

// Note is the reason on decline/reject, and the insurer's payment reference on settle.
public record ClaimActionRequest(string? Note);
