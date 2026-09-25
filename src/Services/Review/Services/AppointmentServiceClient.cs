using System.Net.Http.Headers;
using System.Net.Http.Json;
using LandaDoc.Shared.DTOs;

namespace LandaDoc.Review.Services;

// No service-to-service credential and no gateway exist yet, so the only
// viable option here is to forward the caller's own inbound JWT.
public class AppointmentServiceClient(HttpClient http) : IAppointmentServiceClient
{
    public async Task<AppointmentDto?> GetAppointmentAsync(Guid appointmentId, string bearerToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/appointments/{appointmentId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        var resp = await http.SendAsync(req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<AppointmentDto>() : null;
    }
}
