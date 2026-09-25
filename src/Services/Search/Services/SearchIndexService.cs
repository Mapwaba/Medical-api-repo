using System.Text.Json;
using LandaDoc.Shared.DTOs;
using LandaDoc.Shared.Events;
using StackExchange.Redis;

namespace LandaDoc.Search.Services;

// Redis is the source of truth here — a rebuildable projection driven off
// DoctorApproved/DoctorSuspended/ClinicUpdated/ReviewSubmitted events, not a
// relational store, so there is no EF DbContext behind this service.
public class SearchIndexService(IConnectionMultiplexer redis) : ISearchIndexService
{
    private const string AllKey = "doctors:all";
    private const string ProcessedReviewsKey = "processed:reviews";

    private IDatabase Db => redis.GetDatabase();
    private static string DoctorKey(Guid userId) => $"doctor:{userId}";
    private static string SpecialtyKey(string specialty) => $"doctors:specialty:{specialty.Trim().ToLowerInvariant()}";
    private static string CityKey(string city) => $"doctors:city:{city.Trim().ToLowerInvariant()}";
    private static string ClinicKey(Guid clinicId) => $"doctors:clinic:{clinicId}";

    public async Task UpsertDoctorAsync(DoctorApprovedEvent evt)
    {
        var db = Db;
        var key = DoctorKey(evt.UserId);
        var id = evt.UserId.ToString();

        // Clean up stale set memberships in case specialty/clinics changed since a prior index (e.g. reinstatement)
        var existing = await ReadHashAsync(db, key);
        if (existing.TryGetValue("specialty", out var oldSpecialty) && !string.IsNullOrEmpty(oldSpecialty))
            await db.SetRemoveAsync(SpecialtyKey(oldSpecialty), id);
        var oldClinics = ParseClinics(existing.GetValueOrDefault("clinics"));
        foreach (var oldClinic in oldClinics)
        {
            await db.SetRemoveAsync(ClinicKey(oldClinic.Id), id);
            var stillInCity = evt.Clinics.Any(c => string.Equals(c.City, oldClinic.City, StringComparison.OrdinalIgnoreCase));
            if (!stillInCity) await db.SetRemoveAsync(CityKey(oldClinic.City), id);
        }

        await db.HashSetAsync(key,
        [
            new HashEntry("first_name", evt.FirstName),
            new HashEntry("last_name", evt.LastName),
            new HashEntry("specialty", evt.Specialty),
            new HashEntry("bio", evt.Bio ?? ""),
            new HashEntry("consultation_fee", evt.ConsultationFee.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new HashEntry("clinics", JsonSerializer.Serialize(evt.Clinics)),
        ]);

        await db.SetAddAsync(AllKey, id);
        await db.SetAddAsync(SpecialtyKey(evt.Specialty), id);
        foreach (var clinic in evt.Clinics)
        {
            await db.SetAddAsync(ClinicKey(clinic.Id), id);
            await db.SetAddAsync(CityKey(clinic.City), id);
        }
    }

    public async Task RemoveDoctorAsync(Guid userId)
    {
        var db = Db;
        var key = DoctorKey(userId);
        var id = userId.ToString();
        var existing = await ReadHashAsync(db, key);
        if (existing.Count == 0) return;

        await db.SetRemoveAsync(AllKey, id);
        if (existing.TryGetValue("specialty", out var specialty) && !string.IsNullOrEmpty(specialty))
            await db.SetRemoveAsync(SpecialtyKey(specialty), id);
        foreach (var clinic in ParseClinics(existing.GetValueOrDefault("clinics")))
        {
            await db.SetRemoveAsync(ClinicKey(clinic.Id), id);
            await db.SetRemoveAsync(CityKey(clinic.City), id);
        }
        await db.KeyDeleteAsync(key);
    }

    public async Task UpdateClinicAsync(ClinicUpdatedEvent evt)
    {
        var db = Db;
        var members = await db.SetMembersAsync(ClinicKey(evt.ClinicId));
        foreach (var member in members)
        {
            var doctorKey = DoctorKey(Guid.Parse(member!));
            var map = await ReadHashAsync(db, doctorKey);
            var clinics = ParseClinics(map.GetValueOrDefault("clinics"));
            var existingClinic = clinics.FirstOrDefault(c => c.Id == evt.ClinicId);
            if (existingClinic is null) continue; // stale set membership, ignore

            var oldCity = existingClinic.City;
            var updated = clinics
                .Select(c => c.Id == evt.ClinicId
                    ? new ClinicDto(c.Id, evt.Name, evt.Type, evt.Address, evt.City, c.Phone, c.CreatedAt)
                    : c)
                .ToList();

            await db.HashSetAsync(doctorKey, [new HashEntry("clinics", JsonSerializer.Serialize(updated))]);

            if (!string.Equals(oldCity, evt.City, StringComparison.OrdinalIgnoreCase))
            {
                var stillInOldCity = updated.Any(c => string.Equals(c.City, oldCity, StringComparison.OrdinalIgnoreCase));
                if (!stillInOldCity) await db.SetRemoveAsync(CityKey(oldCity), member);
                await db.SetAddAsync(CityKey(evt.City), member);
            }
        }
    }

    public async Task UpdateRatingAsync(Guid doctorId, Guid reviewId, int rating)
    {
        var db = Db;
        // Redis-native equivalent of the AnyAsync-before-insert guard used by SQL consumers —
        // a redelivered ReviewSubmittedEvent must not double-count the rating.
        var added = await db.SetAddAsync(ProcessedReviewsKey, reviewId.ToString());
        if (!added) return;

        var doctorKey = DoctorKey(doctorId);
        if (!await db.KeyExistsAsync(doctorKey)) return; // unknown/unapproved doctor — ignore

        await db.HashIncrementAsync(doctorKey, "rating_sum", rating);
        await db.HashIncrementAsync(doctorKey, "rating_count", 1);
    }

    public async Task<IReadOnlyList<DoctorSearchResultDto>> SearchAsync(string? specialty, string? city, string? q)
    {
        var db = Db;
        // Specialty/city are free text (typed by doctors on their profile and by patients in
        // the search box), so matching must be substring-based like the name query below —
        // the doctors:specialty:*/doctors:city:* sets only support exact-key membership and
        // would silently return nothing for anything but an exact match.
        var candidateIds = await db.SetMembersAsync(AllKey);

        var results = new List<DoctorSearchResultDto>();
        foreach (var candidateId in candidateIds)
        {
            var dto = await GetByIdAsync(Guid.Parse(candidateId!));
            if (dto is null) continue;
            if (!string.IsNullOrWhiteSpace(specialty) &&
                !dto.Specialty.Contains(specialty, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(city) &&
                !dto.Clinics.Any(c => c.City.Contains(city, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (!string.IsNullOrWhiteSpace(q) &&
                !dto.FirstName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !dto.LastName.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;
            results.Add(dto);
        }
        return results;
    }

    public async Task<DoctorSearchResultDto?> GetByIdAsync(Guid doctorId)
    {
        var map = await ReadHashAsync(Db, DoctorKey(doctorId));
        if (map.Count == 0) return null;

        double.TryParse(map.GetValueOrDefault("rating_sum"), out var ratingSum);
        int.TryParse(map.GetValueOrDefault("rating_count"), out var ratingCount);
        decimal.TryParse(map.GetValueOrDefault("consultation_fee"), System.Globalization.CultureInfo.InvariantCulture, out var consultationFee);

        return new DoctorSearchResultDto(
            doctorId,
            map.GetValueOrDefault("first_name", ""),
            map.GetValueOrDefault("last_name", ""),
            map.GetValueOrDefault("specialty", ""),
            string.IsNullOrEmpty(map.GetValueOrDefault("bio")) ? null : map["bio"],
            consultationFee,
            ParseClinics(map.GetValueOrDefault("clinics")),
            ratingCount > 0 ? ratingSum / ratingCount : 0,
            ratingCount
        );
    }

    private static List<ClinicDto> ParseClinics(string? json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<ClinicDto>>(json) ?? [];

    private static async Task<Dictionary<string, string>> ReadHashAsync(IDatabase db, string key)
    {
        var entries = await db.HashGetAllAsync(key);
        return entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
    }
}
