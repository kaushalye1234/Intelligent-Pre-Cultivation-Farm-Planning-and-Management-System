using System.Globalization;
using System.Text.Json;

namespace AgriAssist.Api.Services.TaskApproval;

/// <summary>An Admin-authored irrigation time and duration from a verified crop reference profile.</summary>
public sealed record IrrigationScheduleReferenceRule(int DayOffsetFromPlanting, string StartTimeUtc, int DurationMinutes)
{
    public const string RuleType = "IrrigationSchedule";

    public static bool TryParse(string json, out IrrigationScheduleReferenceRule? rule, out string error)
    {
        rule = null;
        error = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("dayOffsetFromPlanting", out var offsetValue)
                || !offsetValue.TryGetInt32(out var offset)
                || offset is < 0 or > 365
                || !root.TryGetProperty("startTimeUtc", out var timeValue)
                || timeValue.ValueKind != JsonValueKind.String
                || !TimeOnly.TryParseExact(timeValue.GetString(), "HH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _)
                || !root.TryGetProperty("durationMinutes", out var durationValue)
                || !durationValue.TryGetInt32(out var duration)
                || duration is < 1 or > 1440)
            {
                error = "Expected a 0-365 day offset, HH:mm UTC time, and 1-1440 minute duration.";
                return false;
            }

            rule = new IrrigationScheduleReferenceRule(offset, timeValue.GetString()!, duration);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            error = "Irrigation schedule rule must be valid structured JSON.";
            return false;
        }
    }
}
