using System.Net.Http.Headers;
using System.Text.Json;

namespace LandaDoc.Document.Services;

// Answers "has this doctor had an appointment with this patient?" by asking the Appointment
// service for the doctor's own appointments. No service-to-service credential exists yet, so,
// like the Review service, it forwards the caller's own JWT.
public interface IAppointmentAccessClient
{
    // null when the Appointment service couldn't be reached: callers must deny, not allow
    Task<bool?> DoctorHasSeenPatientAsync(Guid patientId, string bearerToken);
}

public class AppointmentAccessClient(HttpClient http, ILogger<AppointmentAccessClient> logger) : IAppointmentAccessClient
{
    public async Task<bool?> DoctorHasSeenPatientAsync(Guid patientId, string bearerToken)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/appointments/mine");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Appointment service answered {Status} when checking document access", (int)resp.StatusCode);
                return null;
            }

            // Read only patientId and status, so this doesn't depend on how enums are serialized
            using var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            foreach (var appt in json.RootElement.EnumerateArray())
            {
                if (!appt.TryGetProperty("patientId", out var p) || p.GetGuid() != patientId) continue;
                if (!IsCancelled(appt)) return true;
            }
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Couldn't reach the Appointment service to check document access");
            return null;
        }
    }

    private static bool IsCancelled(JsonElement appt) =>
        appt.TryGetProperty("status", out var s) && s.ValueKind switch
        {
            JsonValueKind.String => s.GetString() == "Cancelled",
            JsonValueKind.Number => s.GetInt32() == 3, // AppointmentStatus.Cancelled
            _ => false,
        };
}
