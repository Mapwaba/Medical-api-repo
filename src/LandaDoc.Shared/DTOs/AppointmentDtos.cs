using System.ComponentModel.DataAnnotations;
using LandaDoc.Shared.Models;
namespace LandaDoc.Shared.DTOs;

public record AppointmentDto(
Guid Id, int RefNumber,
Guid DoctorId, Guid PatientId, Guid? ClinicId,
DateTime SlotStart, DateTime SlotEnd,
string? Motif, AppointmentStatus Status, string? Notes,
string? DoctorName, string? Specialty,
string? PatientName, string? ClinicName,
DateTime CreatedAt);
public record CreateAppointmentRequest(
[Required] Guid DoctorId,
[Required] DateTime SlotStart,
string? Motif,
Guid? BookForPatientId); // null => booking for self; otherwise a family member/dependent the caller is authorized for
public record UpdateStatusRequest([Required] string Status);
public record RescheduleRequest([Required] DateTime SlotStart);
public record DoctorAppointmentCountDto(Guid DoctorId, int Count);
public record AppointmentStatsDto(int TotalCount, List<DoctorAppointmentCountDto> ByDoctor);