using System.Net.Http.Headers;
using System.Text.Json;

namespace LandaDoc.Identity.Services;

// Which patients has this doctor had a (non-cancelled) appointment with? Asks the Appointment
// service for the doctor's own appointments, forwarding the caller's JWT (no service-to-service
// credential exists yet — same approach as the Review and Document services).
public interface IAppointmentPatientsClient
{
    // null when the Appointment service couldn't be reached: callers must not guess
    Task<HashSet<Guid>?> GetSeenPatientIdsAsync(string bearerToken);
}

public class AppointmentPatientsClient(HttpClient http, ILogger<AppointmentPatientsClient> logger) : IAppointmentPatientsClient
{
    public async Task<HashSet<Guid>?> GetSeenPatientIdsAsync(string bearerToken)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/appointments/mine");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Appointment service answered {Status} when listing a doctor's patients", (int)resp.StatusCode);
                return null;
            }

            // Read only patientId and status, so this doesn't depend on how enums are serialized
            using var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var ids = new HashSet<Guid>();
            foreach (var appt in json.RootElement.EnumerateArray())
            {
                if (IsCancelled(appt) || !appt.TryGetProperty("patientId", out var p)) continue;
                ids.Add(p.GetGuid());
            }
            return ids;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Couldn't reach the Appointment service to list a doctor's patients");
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
