using LandaDoc.Shared.DTOs;

namespace LandaDoc.Admin.Services;

public interface IClinicService
{
    Task<List<Models.Clinic>> GetAllAsync();
    Task<Models.Clinic?> GetByIdAsync(Guid id);
    Task<Models.Clinic> CreateAsync(CreateClinicRequest req);
    Task<Models.Clinic?> UpdateAsync(Guid id, UpdateClinicRequest req);
}
