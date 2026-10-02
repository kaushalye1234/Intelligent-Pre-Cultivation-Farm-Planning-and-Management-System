using AgriAssist.Api.Models.CropPlanning;

namespace AgriAssist.Api.Services.Resources;

public static class WeatherLocationResolver
{
    public static string Resolve(string? location, string? district)
    {
        var originalLocation = location ?? string.Empty;
        var trimmedLocation = originalLocation.Trim();
        var canonicalDistrict = SriLankanDistricts.Canonicalize(district);
        if (canonicalDistrict is null) return originalLocation;

        var hasDistrict = ContainsNormalizedPart(trimmedLocation, canonicalDistrict);
        var hasCountry = ContainsNormalizedPart(trimmedLocation, "Sri Lanka");

        var parts = new List<string>();
        if (trimmedLocation.Length > 0) parts.Add(trimmedLocation);
        if (!hasDistrict) parts.Add(canonicalDistrict);
        if (!hasCountry) parts.Add("Sri Lanka");
        return string.Join(", ", parts);
    }

    private static bool ContainsNormalizedPart(string value, string expected)
    {
        var normalizedValue = Normalize(value);
        var normalizedExpected = Normalize(expected);
        return $" {normalizedValue} ".Contains($" {normalizedExpected} ", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var comparable = new string(value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray());
        return string.Join(' ', comparable.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
