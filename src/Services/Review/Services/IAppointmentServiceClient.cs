using LandaDoc.Shared.DTOs;

namespace LandaDoc.Review.Services;

public interface IAppointmentServiceClient
{
    Task<AppointmentDto?> GetAppointmentAsync(Guid appointmentId, string bearerToken);
}
