namespace AgriAssist.Api.Services.Shared;

public static class SriLankanPhoneNumber
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var compact = new string(value.Where(character => !char.IsWhiteSpace(character) && character != '-').ToArray());
        var nationalNumber = compact.StartsWith("+94", StringComparison.Ordinal)
            ? compact[3..]
            : compact.StartsWith('0')
                ? compact[1..]
                : string.Empty;

        return nationalNumber.Length == 9
            && nationalNumber[0] is >= '1' and <= '9'
            && nationalNumber.All(char.IsAsciiDigit)
                ? $"+94{nationalNumber}"
                : null;
    }
}
