using System.Globalization;
using System.Net;
using System.Text.Json;
using AgriAssist.Api.Data;
using AgriAssist.Api.Dtos.Resources;
using AgriAssist.Api.Models.CropPlanning;
using AgriAssist.Api.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace AgriAssist.Api.Services.Resources;

public interface ICropResourceRequirementService
{
    Task<CropResourceRequirementsResult> GetRequirementsAsync(Guid cropPlanRequestId, CancellationToken cancellationToken);
    Task<CropResourceRequirementsResult> GetRequirementsAsync(Guid cropPlanRequestId, Guid? requiredProfileId, CancellationToken cancellationToken);
}

/// <summary>
/// GetCropResourceRequirements. Reads verified "ResourceRequirement" rules from the active crop reference
/// profile and calculates required quantity = verified quantity per area x field area. Values are never
/// estimated: a missing rule, field area, unit or invalid rule gives an Unknown requirement with a reason.
/// </summary>
public sealed class CropResourceRequirementService(AppDbContext dbContext, IConfiguration configuration) : ICropResourceRequirementService
{
    public const string FieldAreaUnitConfigurationKey = "Resources:FieldAreaUnit";

    public Task<CropResourceRequirementsResult> GetRequirementsAsync(Guid cropPlanRequestId, CancellationToken cancellationToken) =>
        GetRequirementsAsync(cropPlanRequestId, null, cancellationToken);

    public async Task<CropResourceRequirementsResult> GetRequirementsAsync(Guid cropPlanRequestId, Guid? requiredProfileId, CancellationToken cancellationToken)
    {
        var plan = await dbContext.CropPlanRequests.AsNoTracking()
            .Include(item => item.CropType)
            .Include(item => item.CropVariety)
            .Include(item => item.Field)
            .Include(item => item.Farm)
            .SingleOrDefaultAsync(item => item.Id == cropPlanRequestId && !item.IsDeleted, cancellationToken)
            ?? throw new ApiException(HttpStatusCode.NotFound, "NOT_FOUND", "Crop plan request was not found.");

        var cropName = plan.CropType?.Name ?? string.Empty;
        var varietyName = plan.CropVariety?.Name;
        var field = plan.Field is { IsDeleted: false } ? plan.Field : null;
        var fieldAreaUnit = CropResourceRequirementRule.NormalizeAreaUnit(configuration[FieldAreaUnitConfigurationKey]);

        var now = DateTime.UtcNow;
        var profiles = await dbContext.CropReferenceProfiles.AsNoTracking()
            .Include(profile => profile.Rules)
            .Include(profile => profile.FieldWaterRegimeVerification)
            .Where(profile => profile.CropTypeId == plan.CropTypeId && profile.IsActive && !profile.IsDeleted && profile.VerifiedAt <= now)
            .ToListAsync(cancellationToken);
        var profile = profiles
            .Where(item => !requiredProfileId.HasValue || (item.Id == requiredProfileId.Value && CropReferenceCompatibility.IsVerifiedForPlan(item, plan)))
            .Where(item => item.Rules.Any(IsRequirementRule))
            .Where(item => item.VarietyName is null || string.Equals(item.VarietyName, varietyName, StringComparison.OrdinalIgnoreCase))
            .Where(item => CropReferenceRegionMatcher.Rank(item.Region, plan.Farm) >= 0)
            .OrderByDescending(item => item.VarietyName is not null)
            .ThenByDescending(item => CropReferenceRegionMatcher.Rank(item.Region, plan.Farm))
            .ThenByDescending(item => item.VerifiedAt)
            .ThenBy(item => item.Id)
            .FirstOrDefault();

        if (profile is null)
        {
            var subject = varietyName is null ? cropName : $"{cropName} ({varietyName})";
            return new CropResourceRequirementsResult(plan.Id, plan.CropTypeId, cropName, varietyName, field?.Id, field?.Area, fieldAreaUnit,
                "Unavailable", requiredProfileId.HasValue
                    ? "The required crop reference is missing, inactive, unverified, or incompatible with the field evidence."
                    : $"No verified crop-resource requirement is available for {subject}.", null, []);
        }

        string? areaProblem = field is null
            ? "The crop plan has no field, so the field area is unknown."
            : field.Area <= 0
                ? "The field area is not recorded."
                : fieldAreaUnit is null
                    ? $"The field area unit is not configured ({FieldAreaUnitConfigurationKey})."
                    : null;

        var resources = await dbContext.Resources.AsNoTracking()
            .Where(resource => resource.IsActive && !resource.IsDeleted)
            .Select(resource => new { resource.Id, resource.Name })
            .ToListAsync(cancellationToken);

        var requirements = new List<CalculatedResourceRequirement>();
        foreach (var rule in profile.Rules.Where(IsRequirementRule).OrderBy(item => item.RuleKey).ThenBy(item => item.Id))
        {
            if (!CropResourceRequirementRule.TryParse(rule.StructuredValueJson, out var parsed, out var parseError))
            {
                requirements.Add(new CalculatedResourceRequirement(rule.Id, rule.RuleKey, null, rule.RuleKey, ResourceMatchStatus.Unresolved,
                    null, null, null, null, RequirementCalculationStatus.Unknown, null, $"The verified rule is incomplete: {parseError}"));
                continue;
            }

            var matches = parsed!.ResourceId is { } resourceId
                ? resources.Where(resource => resource.Id == resourceId).ToList()
                : resources.Where(resource => string.Equals(resource.Name.Trim(), parsed.ResourceName, StringComparison.OrdinalIgnoreCase)).ToList();
            var match = matches.Count switch
            {
                0 => ResourceMatchStatus.NotInCatalogue,
                1 => ResourceMatchStatus.Matched,
                _ => ResourceMatchStatus.Ambiguous
            };
            Guid? matchedId = matches.Count == 1 ? matches[0].Id : null;
            var resourceName = matches.Count == 1 ? matches[0].Name
                : string.IsNullOrWhiteSpace(parsed.ResourceName) ? rule.RuleKey : parsed.ResourceName;

            if (areaProblem is not null)
            {
                requirements.Add(new CalculatedResourceRequirement(rule.Id, rule.RuleKey, matchedId, resourceName, match, parsed.QuantityPerArea,
                    parsed.ResourceUnit, parsed.AreaUnit, null, RequirementCalculationStatus.Unknown, null, areaProblem));
                continue;
            }

            var areaInRuleUnit = ConvertArea(field!.Area, fieldAreaUnit!, parsed.AreaUnit);
            var required = Math.Round(parsed.QuantityPerArea * areaInRuleUnit, 3, MidpointRounding.AwayFromZero);
            var basis = $"{Format(parsed.QuantityPerArea)} {parsed.ResourceUnit}/{parsed.AreaUnit} x {Format(Math.Round(areaInRuleUnit, 4))} {parsed.AreaUnit} = {Format(required)} {parsed.ResourceUnit}";
            if (fieldAreaUnit != parsed.AreaUnit) basis += $" (field area {Format(field.Area)} {fieldAreaUnit})";
            requirements.Add(new CalculatedResourceRequirement(rule.Id, rule.RuleKey, matchedId, resourceName, match, parsed.QuantityPerArea,
                parsed.ResourceUnit, parsed.AreaUnit, required, RequirementCalculationStatus.Calculated, basis, null));
        }

        // Two verified rules for one resource cannot be compared with its single stock row safely.
        var duplicates = requirements
            .Where(item => item.ResourceMatch != ResourceMatchStatus.Unresolved)
            .GroupBy(item => item.ResourceId?.ToString() ?? item.ResourceName.Trim().ToLowerInvariant())
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => item.RuleId))
            .ToHashSet();
        requirements = requirements
            .Select(item => duplicates.Contains(item.RuleId)
                ? item with { RequiredQuantity = null, Basis = null, Status = RequirementCalculationStatus.Unknown, Reason = "More than one verified rule exists for this resource; resolve the crop reference data." }
                : item)
            .ToList();

        var calculated = requirements.Count(item => item.Status == RequirementCalculationStatus.Calculated);
        var status = calculated == requirements.Count ? "Available" : "Incomplete";
        var reason = status == "Available" ? null : areaProblem ?? "One or more verified requirements could not be calculated.";
        var source = new RequirementSourceSummary(profile.Id, profile.SourceName, profile.SourceUrl, profile.SourceVersion, profile.VerifiedAt!.Value, profile.Region, profile.VarietyName);
        return new CropResourceRequirementsResult(plan.Id, plan.CropTypeId, cropName, varietyName, field?.Id, field?.Area, fieldAreaUnit,
            status, reason, source, requirements);
    }

    public static decimal ConvertArea(decimal area, string fromUnit, string toUnit) =>
        (fromUnit, toUnit) switch
        {
            _ when fromUnit == toUnit => area,
            (CropResourceRequirementRule.Acre, CropResourceRequirementRule.Hectare) => area * CropResourceRequirementRule.HectaresPerAcre,
            (CropResourceRequirementRule.Hectare, CropResourceRequirementRule.Acre) => area / CropResourceRequirementRule.HectaresPerAcre,
            _ => throw new ArgumentOutOfRangeException(nameof(toUnit), "Unsupported area unit.")
        };

    private static bool IsRequirementRule(CropRuleReference rule) =>
        !rule.IsDeleted && string.Equals(rule.RuleType.Trim(), CropResourceRequirementRule.RuleType, StringComparison.OrdinalIgnoreCase);

    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}

/// <summary>
/// Structured value of a verified crop reference rule with RuleType "ResourceRequirement", e.g.
/// {"resourceName":"Urea","quantityPerArea":100,"resourceUnit":"kg","areaUnit":"acre"}.
/// "resourceId" may be given instead of (or as well as) "resourceName" to pin one inventory resource.
/// </summary>
public sealed record CropResourceRequirementRule(string ResourceName, Guid? ResourceId, decimal QuantityPerArea, string ResourceUnit, string AreaUnit)
{
    public const string RuleType = "ResourceRequirement";
    public const string Acre = "acre";
    public const string Hectare = "hectare";
    /// <summary>Exact by definition: 1 acre = 4046.8564224 square metres.</summary>
    public const decimal HectaresPerAcre = 0.40468564224m;

    public static string? NormalizeAreaUnit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "acre" or "acres" or "ac" => Acre,
        "hectare" or "hectares" or "ha" => Hectare,
        _ => null
    };

    public static bool TryParse(string? json, out CropResourceRequirementRule? rule, out string? error)
    {
        rule = null;
        error = null;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "the value must be a JSON object.";
                return false;
            }

            var name = ReadString(root, "resourceName");
            Guid? resourceId = null;
            if (ReadString(root, "resourceId") is { } idText)
            {
                if (!Guid.TryParse(idText, out var parsedId)) { error = "resourceId must be a GUID."; return false; }
                resourceId = parsedId;
            }
            if (string.IsNullOrWhiteSpace(name) && resourceId is null) { error = "resourceName or resourceId is required."; return false; }
            if (!root.TryGetProperty("quantityPerArea", out var quantity) || quantity.ValueKind != JsonValueKind.Number
                || !quantity.TryGetDecimal(out var quantityPerArea) || quantityPerArea <= 0)
            {
                error = "quantityPerArea must be a positive number.";
                return false;
            }
            var resourceUnit = ReadString(root, "resourceUnit");
            if (string.IsNullOrWhiteSpace(resourceUnit)) { error = "resourceUnit is required."; return false; }
            var areaUnit = NormalizeAreaUnit(ReadString(root, "areaUnit"));
            if (areaUnit is null) { error = "areaUnit must be acre or hectare."; return false; }

            rule = new CropResourceRequirementRule(name?.Trim() ?? string.Empty, resourceId, quantityPerArea, resourceUnit.Trim(), areaUnit);
            return true;
        }
        catch (JsonException)
        {
            error = "the value is not valid JSON.";
            return false;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
