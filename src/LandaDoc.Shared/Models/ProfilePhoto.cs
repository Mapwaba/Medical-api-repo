namespace LandaDoc.Shared.Models;

// Profile pictures are small squares the apps shrink in the browser (PhotoPicker), stored inline
// as data: URLs like clinic logos: on the doctor profile (required for doctors, shown to patients
// in search) and on the account for patients (optional, shown to their doctors).
public static class ProfilePhoto
{
    // Pixels per side the apps shrink a picture to, and the most a stored one may take
    public const int Size = 256;
    public const int MaxLength = 200_000;

    private static readonly string[] Prefixes = ["data:image/jpeg;base64,", "data:image/png;base64,", "data:image/webp;base64,"];

    public static bool IsValid(string? dataUrl) =>
        dataUrl is not null && dataUrl.Length <= MaxLength && Prefixes.Any(p => dataUrl.StartsWith(p, StringComparison.Ordinal));
}
