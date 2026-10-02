namespace AgriAssist.Api.Models.CropPlanning;

public static class SriLankanDistricts
{
    public static readonly IReadOnlyList<string> All =
    [
        "Ampara",
        "Anuradhapura",
        "Badulla",
        "Batticaloa",
        "Colombo",
        "Galle",
        "Gampaha",
        "Hambantota",
        "Jaffna",
        "Kalutara",
        "Kandy",
        "Kegalle",
        "Kilinochchi",
        "Kurunegala",
        "Mannar",
        "Matale",
        "Matara",
        "Monaragala",
        "Mullaitivu",
        "Nuwara Eliya",
        "Polonnaruwa",
        "Puttalam",
        "Ratnapura",
        "Trincomalee",
        "Vavuniya"
    ];

    private static readonly IReadOnlyDictionary<string, string> CanonicalNames = All
        .ToDictionary(district => district, StringComparer.OrdinalIgnoreCase);

    public static string? Canonicalize(string? value)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrEmpty(trimmed) && CanonicalNames.TryGetValue(trimmed, out var canonical)
            ? canonical
            : null;
    }
}
