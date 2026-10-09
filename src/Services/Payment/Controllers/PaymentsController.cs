using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using LandaDoc.Payment.Data;
using LandaDoc.Payment.Services;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using LandaDoc.Shared.Models;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using StripeEvent = Stripe.Event;

namespace LandaDoc.Payment.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController(
    PaymentDbContext db,
    IConfiguration cfg,
    IPublishEndpoint bus,
    IMokoAfrikaClient moko,
    ILogger<PaymentsController> log) : ControllerBase
{
    // Stripe calls this endpoint when payment succeeds or fails
    [HttpPost("webhook/stripe")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"];
        var secret = cfg["Stripe:WebhookSecret"]!;
        StripeEvent stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, secret);
        }
        catch (StripeException)
        {
            log.LogWarning("Stripe webhook rejected: invalid signature");
            return BadRequest();
        }

        if (stripeEvent.Type == Events.PaymentIntentSucceeded)
        {
            var intent = (PaymentIntent)stripeEvent.Data.Object;
            var paymentId = Guid.Parse(intent.Metadata["payment_id"]);
            var payment = await db.Payments.FindAsync(paymentId);
            if (payment is null) return Ok();

            // Stripe retries webhook delivery — guard the insert-only ledger against duplicates
            var alreadyCompleted = await db.Payments
                .AnyAsync(p => p.ProviderRef == intent.Id);
            if (alreadyCompleted) return Ok();

            // Insert a new Completed row (immutable ledger — no updates)
            db.Payments.Add(new Models.Payment
            {
                AppointmentId = payment.AppointmentId,
                DoctorId = payment.DoctorId,
                PatientId = payment.PatientId,
                GrossAmount = payment.GrossAmount,
                PlatformFee = payment.PlatformFee,
                NetAmount = payment.NetAmount,
                Status = PaymentStatus.Completed,
                Provider = PaymentProvider.Stripe,
                ProviderRef = intent.Id,
            });
            await db.SaveChangesAsync();
            await bus.Publish(new PaymentCompletedEvent(
                payment.AppointmentId, payment.DoctorId,
                payment.PatientId, DateTime.UtcNow));
            log.LogInformation("Card payment succeeded for appointment {AppointmentId}: {Amount} (Stripe {ProviderRef})",
                payment.AppointmentId, payment.GrossAmount, intent.Id);
        }
        else if (stripeEvent.Type == Events.PaymentIntentPaymentFailed)
        {
            var intent = (PaymentIntent)stripeEvent.Data.Object;
            var paymentId = Guid.Parse(intent.Metadata["payment_id"]);
            var payment = await db.Payments.FindAsync(paymentId);
            if (payment is null) return Ok();

            // Stripe retries webhook delivery — guard the insert-only ledger against duplicates
            var alreadyFailed = await db.Payments
                .AnyAsync(p => p.ProviderRef == intent.Id);
            if (alreadyFailed) return Ok();

            var reason = intent.LastPaymentError?.Message;

            // Insert a new Failed row (immutable ledger — no updates)
            db.Payments.Add(new Models.Payment
            {
                AppointmentId = payment.AppointmentId,
                DoctorId = payment.DoctorId,
                PatientId = payment.PatientId,
                GrossAmount = payment.GrossAmount,
                PlatformFee = payment.PlatformFee,
                NetAmount = payment.NetAmount,
                Status = PaymentStatus.Failed,
                Provider = PaymentProvider.Stripe,
                ProviderRef = intent.Id,
            });
            await db.SaveChangesAsync();
            await bus.Publish(new PaymentFailedEvent(
                payment.AppointmentId, reason, DateTime.UtcNow));
            log.LogWarning("Card payment failed for appointment {AppointmentId} (Stripe {ProviderRef}): {Reason}",
                payment.AppointmentId, intent.Id, reason);
        }
        return Ok();
    }

    // Moko Afrika (FreshPay) calls this endpoint after the customer approves or rejects the
    // mobile money prompt. Two shapes arrive:
    //  - {"data": "<AES-encrypted JSON>"} with an HMAC X-Signature header: trusted once checked;
    //  - the plain JSON FreshPay's PayDRC document shows ({"Reference":..., "Trans_Status":...}):
    //    unsigned, so anyone could post it — only its reference is used, and the outcome recorded
    //    is the one FreshPay's verify action returns.
    // Callbacks FreshPay never manages to deliver are caught by MobileMoneyReconciliationService.
    [HttpPost("webhook/moko")]
    [AllowAnonymous]
    public async Task<IActionResult> MokoWebhook([FromServices] MobileMoneySettlement settlement, CancellationToken ct)
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync(ct);

        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            return BadRequest();
        }
        if (root.ValueKind != JsonValueKind.Object) return BadRequest();

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
            return await SignedMokoCallbackAsync(data.GetString()!, settlement, ct);

        var callback = MokoCallback.From(root);
        if (callback.Reference is null) return BadRequest();
        return await PlainMokoCallbackAsync(callback, settlement, ct);
    }

    private async Task<IActionResult> SignedMokoCallbackAsync(string encryptedData, MobileMoneySettlement settlement, CancellationToken ct)
    {
        var signature = Request.Headers["X-Signature"].ToString();
        if (string.IsNullOrEmpty(signature) || !moko.VerifySignature(encryptedData, signature))
        {
            log.LogWarning("Moko Afrika webhook rejected: missing or invalid signature");
            return Unauthorized();
        }

        MokoCallback callback;
        try
        {
            callback = MokoCallback.From(JsonSerializer.Deserialize<JsonElement>(moko.Decrypt(encryptedData)));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return BadRequest();
        }
        if (callback.Reference is null) return Ok();

        await settlement.ApplyAsync(callback.Reference, callback.TransactionId, callback.TransStatus, callback.Description, "signed callback", ct);
        return Ok(new { status = "Callback received successfully" });
    }

    private async Task<IActionResult> PlainMokoCallbackAsync(MokoCallback callback, MobileMoneySettlement settlement, CancellationToken ct)
    {
        // Only references of ours are worth a call to FreshPay
        if (MobileMoneySettlement.PaymentIdFrom(callback.Reference!) is not { } paymentId
            || !await db.Payments.AnyAsync(p => p.Id == paymentId, ct))
            return Ok();

        // Ask FreshPay by our reference, then by its own id if it doesn't find that
        var verified = await moko.VerifyAsync(callback.Reference!, ct);
        if (!verified.Found && callback.TransactionId is { } providerId)
            verified = await moko.VerifyAsync(providerId, ct);
        if (!verified.Found)
        {
            log.LogWarning("Moko Afrika callback for {Reference} not confirmed by FreshPay ({Reason}); left for reconciliation",
                callback.Reference, verified.Description);
            return Ok();
        }

        if (!string.Equals(verified.TransStatus, callback.TransStatus, StringComparison.OrdinalIgnoreCase))
            log.LogWarning("Moko Afrika callback for {Reference} said {CallbackStatus} but FreshPay says {VerifiedStatus}; recording FreshPay's",
                callback.Reference, callback.TransStatus, verified.TransStatus);

        await settlement.ApplyAsync(callback.Reference!, verified.TransactionId ?? callback.TransactionId,
            verified.TransStatus, verified.Description ?? callback.Description, "plain callback, verified", ct);
        return Ok(new { status = "Callback received successfully" });
    }

    [HttpPost("{id:guid}/initiate")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Initiate(Guid id, [FromBody] InitiatePaymentRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();

        // The patient pays, or the family member who booked for them (always the case for a
        // dependant, who has no account)
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (payment.PatientId != callerId && payment.BookedByUserId != callerId) return Forbid();
        if (payment.Status != PaymentStatus.Pending)
            return Conflict(new { error = "Payment is not in a payable state" });

        // A claim waiting for the doctor's review already covers this booking
        var claimUnderReview = await db.InsuranceClaims.AnyAsync(c =>
            c.PaymentId == payment.Id && c.Status == InsuranceClaimStatus.Submitted);
        if (claimUnderReview)
            return Conflict(new { error = "An insurance claim for this booking is waiting for the doctor's review" });

        // Card isn't offered where the payer lives (DRC: mobile money or insurance only)
        if (!PaymentMethods.IsAllowed(request.Provider, User.FindFirstValue(PaymentMethods.CountryClaim)))
            return BadRequest(new { error = "This payment method isn't available in your country" });

        return request.Provider switch
        {
            PaymentProvider.Stripe => await InitiateStripeAsync(payment),
            PaymentProvider.MokoAfrika => await InitiateMokoAsync(payment, request),
            PaymentProvider.Insurance => await InitiateInsuranceAsync(payment, request),
            _ => BadRequest()
        };
    }

    // No money moves now: the claim goes to the doctor, who checks the patient's cover and
    // approves it (booking confirmed, insurer billed later) or declines it.
    private async Task<IActionResult> InitiateInsuranceAsync(Models.Payment payment, InitiatePaymentRequest request)
    {
        if (request.InsurerId is null || string.IsNullOrWhiteSpace(request.MemberNumber))
            return BadRequest(new { error = "Insurer and member number are required for insurance" });

        var insurer = await db.Insurers.FirstOrDefaultAsync(i => i.Id == request.InsurerId && i.IsActive);
        if (insurer is null)
            return BadRequest(new { error = "This insurer isn't accepted on LandaDoc" });

        var choice = await db.DoctorInsurerChoices.FindAsync(payment.DoctorId);
        if (choice is not null && !choice.InsurerIds.Contains(insurer.Id))
            return BadRequest(new { error = "This doctor doesn't accept this insurer" });

        // The claim covers the full consultation fee; the insurer's name is copied so the claim
        // still reads correctly if an admin renames the insurer later
        var claim = new Models.InsuranceClaim
        {
            PaymentId = payment.Id,
            AppointmentId = payment.AppointmentId,
            DoctorId = payment.DoctorId,
            PatientId = payment.PatientId,
            InsurerId = insurer.Id,
            InsurerName = insurer.Name,
            MemberNumber = request.MemberNumber.Trim(),
            MemberName = string.IsNullOrWhiteSpace(request.MemberName) ? null : request.MemberName.Trim(),
            Amount = payment.GrossAmount,
        };
        db.InsuranceClaims.Add(claim);
        await db.SaveChangesAsync();

        // Appointment stops the unpaid-booking expiry; Notification alerts the doctor
        await bus.Publish(new InsuranceClaimSubmittedEvent(
            payment.AppointmentId, payment.DoctorId, payment.PatientId, insurer.Name, DateTime.UtcNow));
        log.LogInformation("Insurance claim {ClaimId} filed for appointment {AppointmentId} with insurer {InsurerId} ({Amount}); waiting for doctor {DoctorId}",
            claim.Id, claim.AppointmentId, claim.InsurerId, claim.Amount, claim.DoctorId);

        return Ok(new InitiatePaymentResponse(
            PaymentProvider.Insurance, null, "Your claim was sent to the doctor for review."));
    }

    private async Task<IActionResult> InitiateStripeAsync(Models.Payment payment)
    {
        var frontendUrl = cfg["Frontend:PatientBaseUrl"]!.TrimEnd('/');
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = (long)(payment.GrossAmount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Consultation #{payment.AppointmentId}"
                        }
                    }
                }
            ],
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string> { ["payment_id"] = payment.Id.ToString() }
            },
            SuccessUrl = $"{frontendUrl}/payments/{payment.Id}/result?status=success",
            CancelUrl = $"{frontendUrl}/payments/{payment.Id}/result?status=cancelled"
        };
        var session = await new SessionService().CreateAsync(options);
        log.LogInformation("Card checkout opened for appointment {AppointmentId}: {Amount} (Stripe session {SessionId})",
            payment.AppointmentId, payment.GrossAmount, session.Id);
        return Ok(new InitiatePaymentResponse(PaymentProvider.Stripe, session.Url, null));
    }

    private async Task<IActionResult> InitiateMokoAsync(Models.Payment payment, InitiatePaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.Operator is null
            || string.IsNullOrWhiteSpace(request.PatientFullName))
        {
            return BadRequest(new { error = "Phone number, operator, and full name are required for mobile money" });
        }

        var nameParts = request.PatientFullName.Trim().Split(' ', 2);
        var firstName = nameParts[0];
        var lastName = nameParts.Length > 1 ? nameParts[1] : nameParts[0];
        var email = User.FindFirstValue(ClaimTypes.Email) ?? $"{payment.PatientId}@landadoc.local";

        var callbackBase = cfg["MokoAfrika:CallbackBaseUrl"]!.TrimEnd('/');
        // A fresh reference per attempt (Moko requires uniqueness for retries); the leading
        // segment is payment.Id in "N" format so the webhook can correlate the callback back
        // to this ledger row without any extra state.
        var reference = $"{payment.Id:N}_{Guid.NewGuid():N}";

        var result = await moko.InitiateDebitAsync(
            reference, payment.GrossAmount, request.PhoneNumber, request.Operator.Value,
            firstName, lastName, email, $"{callbackBase}/api/payments/webhook/moko");

        if (!result.Success)
        {
            log.LogWarning("Moko Afrika refused to start a payment for appointment {AppointmentId}: {Comment}",
                payment.AppointmentId, result.Comment);
            return UnprocessableEntity(new { error = result.Comment ?? "Could not start the mobile money payment" });
        }

        // Remembered so the reconciliation job can ask FreshPay about it if no callback comes
        db.MobileMoneyAttempts.Add(new Models.MobileMoneyAttempt
        {
            PaymentId = payment.Id,
            Reference = reference,
            ProviderTransactionId = result.TransactionId,
            Operator = request.Operator.Value,
            Amount = payment.GrossAmount,
        });
        await db.SaveChangesAsync();

        log.LogInformation("Mobile money prompt sent for appointment {AppointmentId}: {Amount} via {Operator}",
            payment.AppointmentId, payment.GrossAmount, request.Operator);
        return Ok(new InitiatePaymentResponse(
            PaymentProvider.MokoAfrika, null, "Check your phone to approve the payment."));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();

        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (payment.PatientId != callerId && payment.DoctorId != callerId && !await BookedByAsync(payment.AppointmentId, callerId))
            return Forbid();

        return Ok(MapToDto(payment));
    }

    // Every payment for the calling doctor's appointments, newest first (Doctor app's Payments page)
    [HttpGet("doctor/me")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> GetMineAsDoctor()
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var payments = await db.Payments
            .Where(p => p.DoctorId == callerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return Ok(payments.Select(MapToDto));
    }

    [HttpGet("by-appointment/{appointmentId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetByAppointment(Guid appointmentId)
    {
        var payment = await db.Payments
            .Where(p => p.AppointmentId == appointmentId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
        if (payment is null) return NotFound();

        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (payment.PatientId != callerId && payment.DoctorId != callerId && !await BookedByAsync(appointmentId, callerId))
            return Forbid();

        return Ok(MapToDto(payment));
    }

    // Whether the caller booked this appointment for a family member (recorded on the row opened
    // at booking; later rows of the ledger don't repeat it)
    private Task<bool> BookedByAsync(Guid appointmentId, Guid callerId) =>
        db.Payments.AnyAsync(p => p.AppointmentId == appointmentId && p.BookedByUserId == callerId);

    private static PaymentDto MapToDto(Models.Payment p) => new(
        p.Id, p.AppointmentId, p.DoctorId, p.PatientId,
        p.GrossAmount, p.PlatformFee, p.NetAmount, p.Status, p.Provider, p.ProviderRef, p.CreatedAt);
}
