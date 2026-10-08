using LandaDoc.Notification.Services;
using LandaDoc.Shared.Events;
using MassTransit;

namespace LandaDoc.Notification.Consumers;

// A patient wants to pay through their insurer — the doctor has to review the claim
public class InsuranceClaimSubmittedConsumer(INotificationPublisher publisher) : IConsumer<InsuranceClaimSubmittedEvent>
{
    public async Task Consume(ConsumeContext<InsuranceClaimSubmittedEvent> ctx)
    {
        var msg = ctx.Message;

        await publisher.PublishAsync(msg.DoctorId, "insurance_claim_submitted", "Prise en charge à vérifier",
            $"Un patient souhaite payer avec son assurance ({msg.InsurerName}). Vérifiez sa couverture auprès de l'assureur, puis répondez dans Paiements. Sans réponse à temps, la demande sera refusée automatiquement.");
        await publisher.PublishAsync(msg.PatientId, "insurance_claim_submitted", "Demande de prise en charge envoyée",
            $"Votre demande de prise en charge par {msg.InsurerName} a été envoyée au médecin.");
    }
}
