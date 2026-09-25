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
public record InitiatePaymentRequest(PaymentProvider Provider, string? PhoneNumber, MobileMoneyOperator? Operator, string? PatientFullName);

public record InitiatePaymentResponse(PaymentProvider Provider, string? CheckoutUrl, string? Message);
