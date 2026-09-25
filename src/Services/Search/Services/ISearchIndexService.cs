using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;

namespace LandaDoc.Search.Services;

public interface ISearchIndexService
{
    Task UpsertDoctorAsync(DoctorApprovedEvent evt);
    Task RemoveDoctorAsync(Guid userId);
    Task UpdateClinicAsync(ClinicUpdatedEvent evt);
    Task UpdateRatingAsync(Guid doctorId, Guid reviewId, int rating);
    Task<IReadOnlyList<DoctorSearchResultDto>> SearchAsync(string? specialty, string? city, string? q);
    Task<DoctorSearchResultDto?> GetByIdAsync(Guid doctorId);
}
