using AgriAssist.Api.Models.Shared;

namespace AgriAssist.Api.Models.Inspections;

/// <summary>
/// Records one fingerprinted inspection-image analysis operation. A running row may be
/// terminalized once; terminal payloads are immutable application records.
/// </summary>
public sealed class InspectionImageAnalysis : AuditableEntity
{
    public Guid FieldInspectionId { get; set; }
    public FieldInspection? FieldInspection { get; set; }
    public Guid InspectionImageId { get; set; }
    public InspectionImage? InspectionImage { get; set; }
    public string AnalysisFingerprint { get; set; } = string.Empty;
    public InspectionImageAnalysisStatus Status { get; set; } = InspectionImageAnalysisStatus.Running;
    public Guid? CropTypeId { get; set; }
    public Guid? CropVarietyId { get; set; }
    public string InputSnapshotJson { get; set; } = "{}";
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SourcePolicyVersion { get; set; } = string.Empty;
    public string SourcePolicyHash { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public int PromptContractVersion { get; set; }
    public int ImagePreprocessingVersion { get; set; }
    public int RelevanceRuleVersion { get; set; }
    public string? Pass1ResultJson { get; set; }
    public string? EvidencePacketJson { get; set; }
    public string? FinalResultJson { get; set; }
    public string? FailureCategory { get; set; }
    public string? FailureMessageSafe { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Version { get; set; } = 1;

    public bool IsTerminal => Status is not InspectionImageAnalysisStatus.Running;
}

public enum InspectionImageAnalysisStatus
{
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    TimedOut = 4,
    Interrupted = 5,
    Stale = 6
}
