using LandaDoc.Shared.Models;
using LandaDoc.Shared.DTOs;

namespace LandaDoc.Admin.Services;

public enum DoctorProfileResultStatus
{
    Success,
    NotFound,
    AlreadyExists,
    InvalidTransition
}

public record DoctorProfileResult(DoctorProfileResultStatus Status, Models.DoctorProfile? Profile = null);

public interface IDoctorProfileService
{
    Task<List<Models.DoctorProfile>> GetAllAsync(DoctorApprovalStatus? status);
    Task<Models.DoctorProfile?> GetByIdAsync(Guid id);
    Task<Models.DoctorProfile?> GetByUserIdAsync(Guid userId);
    Task<DoctorProfileResult> CreateAsync(CreateDoctorProfileRequest req, bool autoApprove = false);
    Task<DoctorProfileResult> UpdateOwnAsync(Guid userId, UpdateOwnDoctorProfileRequest req);
    Task<DoctorProfileResult> ApproveAsync(Guid id);
    Task<DoctorProfileResult> SuspendAsync(Guid id, SuspendDoctorRequest req);
}
