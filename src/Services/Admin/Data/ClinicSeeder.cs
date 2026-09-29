using LandaDoc.Admin.Models;
using LandaDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Admin.Data;

// Adds the standard Kinshasa clinics on startup. Matching is by name, so it's
// safe to run on every boot and never touches clinics an admin has edited.
// No ClinicUpdatedEvent is published: a new clinic has no doctors yet, so the
// Search index has nothing to update.
public static class ClinicSeeder
{
    private static readonly (string Name, ClinicType Type, string City)[] Clinics =
    [
        ("Clinique Odia", ClinicType.Clinic, "Kinshasa"),
        ("Hopital Diamant", ClinicType.Hospital, "Kinshasa"),
        ("Hopital Maman Yemo", ClinicType.Hospital, "Kinshasa"),
        ("Clinique Universitaire", ClinicType.Clinic, "Kinshasa"),
        ("Hopital Mokole", ClinicType.Hospital, "Kinshasa"),
    ];

    public static async Task SeedAsync(AdminDbContext db)
    {
        var names = Clinics.Select(c => c.Name).ToList();
        var existing = await db.Clinics
            .Where(c => names.Contains(c.Name))
            .Select(c => c.Name)
            .ToListAsync();

        foreach (var (name, type, city) in Clinics.Where(c => !existing.Contains(c.Name)))
            db.Clinics.Add(new Clinic { Name = name, Type = type, City = city });

        await db.SaveChangesAsync();
    }
}
