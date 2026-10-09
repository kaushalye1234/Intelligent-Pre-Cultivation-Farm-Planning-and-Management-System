using AgriAssist.Api.Models.CropPlanning;

namespace AgriAssist.Api.Services.Resources;

/// <summary>Shared profile-region precedence for resource analysis and scheduling evidence.</summary>
public static class CropReferenceRegionMatcher
{
    /// <summary>
    /// A district/location match ranks above a nationwide or unspecified profile. An unrelated region is excluded.
    /// </summary>
    public static int Rank(string? region, Farm? farm)
    {
        if (string.IsNullOrWhiteSpace(region) || IsNationwide(region)) return 0;
        var trimmed = region.Trim();
        var district = SriLankanDistricts.Canonicalize(farm?.District);
        if (district is not null && district == SriLankanDistricts.Canonicalize(trimmed)) return 1;
        var location = farm?.Location;
        var matchesLocation = !string.IsNullOrWhiteSpace(location)
            && location.Split(',').Select(part => part.Trim()).Append(location.Trim())
                .Any(part => string.Equals(part, trimmed, StringComparison.OrdinalIgnoreCase));
        return matchesLocation ? 1 : -1;
    }

    private static bool IsNationwide(string region)
    {
        var bracket = region.IndexOf('(');
        var name = bracket >= 0 ? region[..bracket] : region;
        return string.Equals(name.Trim(), "Sri Lanka", StringComparison.OrdinalIgnoreCase);
    }
}
