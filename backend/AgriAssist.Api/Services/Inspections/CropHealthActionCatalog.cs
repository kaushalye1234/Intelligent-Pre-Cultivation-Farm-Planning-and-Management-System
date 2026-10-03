using AgriAssist.Api.Dtos.Inspections;

namespace AgriAssist.Api.Services.Inspections;

public sealed record CropHealthActionDefinition(string Title, string Description, string TimingCategory, string ResponsibleRole);

public static class CropHealthActionCatalog
{
    private static readonly IReadOnlyDictionary<CropHealthActionType, CropHealthActionDefinition> Definitions =
        new Dictionary<CropHealthActionType, CropHealthActionDefinition>
        {
            [CropHealthActionType.FieldSanitation] = new("Complete field sanitation", "Clean the field and remove visibly affected crop material before planting.", "BeforePlanting", "Farmer"),
            [CropHealthActionType.RemoveAffectedResidue] = new("Remove affected crop residues", "Remove visibly affected crop residues from the field before planting.", "BeforePlanting", "Farmer"),
            [CropHealthActionType.SeparateAffectedMaterial] = new("Separate affected crop material", "Keep visibly affected crop material separate from healthy planting material.", "BeforePlanting", "Farmer"),
            [CropHealthActionType.InspectNearbyPlants] = new("Inspect nearby plants", "Inspect nearby plants for similar visible symptoms.", "AsNeededAssessment", "FieldOfficer"),
            [CropHealthActionType.MonitorSymptoms] = new("Monitor crop symptoms", "Monitor the crop during early growth for recurring or spreading symptoms.", "EarlyGrowth", "Farmer"),
            [CropHealthActionType.PrePlantingCleanup] = new("Complete pre-planting cleanup", "Complete crop-health-related cleanup before planting.", "BeforePlanting", "Farmer"),
            [CropHealthActionType.RequestFurtherAssessment] = new("Request further crop-health assessment", "Request further Field Officer or Agricultural Officer assessment if symptoms persist, spread, or remain uncertain.", "AsNeededAssessment", "FieldOfficer")
        };

    public static CropHealthActionDefinition Get(CropHealthActionType actionType) =>
        Definitions.TryGetValue(actionType, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(actionType), actionType, "Unknown crop-health action type.");

    public static IReadOnlyCollection<CropHealthActionType> AllowedActionTypes => Definitions.Keys.ToArray();
}
