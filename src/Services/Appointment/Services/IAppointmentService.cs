using LandaDoc.Shared.DTOs;

namespace LandaDoc.Appointment.Services;

public enum CreateAppointmentResultStatus
{
    Success,
    SlotConflict,
    Forbidden
}

public record CreateAppointmentResult(CreateAppointmentResultStatus Status, Models.Appointment? Appointment = null);

public enum CompleteAppointmentResultStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidStatus
}

public record CompleteAppointmentResult(CompleteAppointmentResultStatus Status, Models.Appointment? Appointment = null);

public enum RescheduleAppointmentResultStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidStatus,
    SlotConflict
}

public record RescheduleAppointmentResult(RescheduleAppointmentResultStatus Status, Models.Appointment? Appointment = null);

public interface IAppointmentService
{
    Task<CreateAppointmentResult> CreateAsync(Guid callerId, CreateAppointmentRequest req);
    Task<Models.Appointment?> GetByIdAsync(Guid id);
    Task<List<Models.Appointment>> GetMineAsync(Guid callerId, string role);
    Task<CompleteAppointmentResult> CompleteAsync(Guid appointmentId, Guid doctorId);
    Task<RescheduleAppointmentResult> RescheduleAsync(Guid appointmentId, Guid patientId, RescheduleRequest req);
    Task<AppointmentStatsDto> GetStatsByDoctorAsync();
}
