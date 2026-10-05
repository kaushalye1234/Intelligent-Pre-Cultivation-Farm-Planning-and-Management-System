class UserProfile {
  const UserProfile({
    required this.id,
    required this.fullName,
    required this.email,
    required this.role,
    required this.isActive,
    this.mustChangePassword = false,
  });

  final String id;
  final String fullName;
  final String email;
  final int role;
  final bool isActive;
  final bool mustChangePassword;

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      id: json['id'] as String,
      fullName: json['fullName'] as String,
      email: json['email'] as String,
      role: json['role'] as int,
      isActive: json['isActive'] as bool,
      mustChangePassword: json['mustChangePassword'] as bool? ?? false,
    );
  }
}

class AuthenticationSession {
  const AuthenticationSession({
    required this.authenticationStatus,
    required this.user,
    this.accessToken,
    this.passwordChangeToken,
  });

  final String authenticationStatus;
  final String? accessToken;
  final String? passwordChangeToken;
  final UserProfile user;

  bool get requiresPasswordChange =>
      authenticationStatus == 'passwordChangeRequired';

  factory AuthenticationSession.fromJson(Map<String, dynamic> json) {
    return AuthenticationSession(
      authenticationStatus: json['authenticationStatus'] as String,
      accessToken: json['accessToken'] as String?,
      passwordChangeToken: json['passwordChangeToken'] as String?,
      user: UserProfile.fromJson(json['user'] as Map<String, dynamic>),
    );
  }
}

class FarmerOnboardingStatus {
  const FarmerOnboardingStatus({
    required this.stage,
    required this.activeFarmCount,
    required this.activeFieldCount,
  });

  final String stage;
  final int activeFarmCount;
  final int activeFieldCount;

  factory FarmerOnboardingStatus.fromJson(Map<String, dynamic> json) {
    return FarmerOnboardingStatus(
      stage: json['stage'] as String,
      activeFarmCount: json['activeFarmCount'] as int? ?? 0,
      activeFieldCount: json['activeFieldCount'] as int? ?? 0,
    );
  }
}

class DashboardSummary {
  const DashboardSummary({
    required this.activeFarms,
    required this.activeCropPlans,
    required this.openCropIssues,
    required this.lowStockResources,
    required this.pendingTasks,
    required this.pendingApprovals,
  });

  final int activeFarms;
  final int activeCropPlans;
  final int openCropIssues;
  final int lowStockResources;
  final int pendingTasks;
  final int pendingApprovals;

  factory DashboardSummary.empty() {
    return const DashboardSummary(
      activeFarms: 0,
      activeCropPlans: 0,
      openCropIssues: 0,
      lowStockResources: 0,
      pendingTasks: 0,
      pendingApprovals: 0,
    );
  }

  factory DashboardSummary.fromJson(Map<String, dynamic> json) {
    return DashboardSummary(
      activeFarms: json['activeFarms'] as int? ?? 0,
      activeCropPlans: json['activeCropPlans'] as int? ?? 0,
      openCropIssues: json['openCropIssues'] as int? ?? 0,
      lowStockResources: json['lowStockResources'] as int? ?? 0,
      pendingTasks: json['pendingTasks'] as int? ?? 0,
      pendingApprovals: json['pendingApprovals'] as int? ?? 0,
    );
  }
}

class FarmTaskRecord {
  const FarmTaskRecord({
    required this.id,
    required this.title,
    required this.description,
    required this.dueAt,
    required this.status,
    this.generatedByWorkflowId,
  });

  final String id;
  final String title;
  final String description;
  final String dueAt;
  final int status;
  final String? generatedByWorkflowId;

  factory FarmTaskRecord.fromJson(Map<String, dynamic> json) => FarmTaskRecord(
    id: json['id'] as String,
    title: json['title'] as String? ?? '',
    description: json['description'] as String? ?? '',
    dueAt: json['dueAt'] as String? ?? '',
    status: json['status'] as int? ?? 0,
    generatedByWorkflowId: json['generatedByWorkflowId'] as String?,
  );

  String get statusLabel => switch (status) {
    1 => 'Draft',
    2 => 'Pending approval',
    3 => 'Approved',
    4 => 'Rejected',
    5 => 'Revision requested',
    6 => 'Completed',
    7 => 'Cancelled',
    _ => 'Unknown',
  };
}

class IrrigationScheduleRecord {
  const IrrigationScheduleRecord({
    required this.id,
    required this.fieldId,
    required this.scheduledAt,
    required this.durationMinutes,
    required this.notes,
    required this.status,
    this.generatedByWorkflowId,
  });

  final String id;
  final String fieldId;
  final String scheduledAt;
  final int durationMinutes;
  final String notes;
  final int status;
  final String? generatedByWorkflowId;

  factory IrrigationScheduleRecord.fromJson(Map<String, dynamic> json) =>
      IrrigationScheduleRecord(
        id: json['id'] as String,
        fieldId: json['fieldId'] as String,
        scheduledAt: json['scheduledAt'] as String? ?? '',
        durationMinutes: json['durationMinutes'] as int? ?? 0,
        notes: json['notes'] as String? ?? '',
        status: json['status'] as int? ?? 0,
        generatedByWorkflowId: json['generatedByWorkflowId'] as String?,
      );

  String get statusLabel => switch (status) {
    1 => 'Pending approval',
    2 => 'Approved',
    3 => 'Rejected',
    4 => 'Revision requested',
    5 => 'Completed',
    6 => 'Cancelled',
    _ => 'Unknown',
  };
}

class ApprovalHistoryRecord {
  const ApprovalHistoryRecord({
    required this.id,
    required this.decision,
    required this.comment,
    required this.createdAt,
  });

  final String id;
  final int decision;
  final String comment;
  final String createdAt;

  factory ApprovalHistoryRecord.fromJson(Map<String, dynamic> json) =>
      ApprovalHistoryRecord(
        id: json['id'] as String,
        decision: json['decision'] as int? ?? 0,
        comment: json['comment'] as String? ?? '',
        createdAt: json['createdAt'] as String? ?? '',
      );

  String get decisionLabel => switch (decision) {
    1 => 'Approved',
    2 => 'Rejected',
    3 => 'Revision requested',
    4 => 'Cancelled',
    _ => 'Pending',
  };
}

class FarmOption {
  const FarmOption({
    required this.id,
    required this.name,
    this.location = '',
    this.district,
    this.totalArea = 0,
  });

  final String id;
  final String name;
  final String location;
  final String? district;
  final num totalArea;

  factory FarmOption.fromJson(Map<String, dynamic> json) {
    return FarmOption(
      id: json['id'] as String,
      name: json['name'] as String,
      location: json['location'] as String? ?? '',
      district: json['district'] as String?,
      totalArea: json['totalArea'] as num? ?? 0,
    );
  }
}

class FieldOption {
  const FieldOption({
    required this.id,
    required this.farmId,
    required this.name,
    required this.isActive,
  });

  final String id;
  final String farmId;
  final String name;
  final bool isActive;

  factory FieldOption.fromJson(Map<String, dynamic> json) {
    return FieldOption(
      id: json['id'] as String,
      farmId: json['farmId'] as String,
      name: json['name'] as String,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}

class CropTypeOption {
  const CropTypeOption({
    required this.id,
    required this.name,
    required this.isActive,
  });

  final String id;
  final String name;
  final bool isActive;

  factory CropTypeOption.fromJson(Map<String, dynamic> json) {
    return CropTypeOption(
      id: json['id'] as String,
      name: json['name'] as String,
      isActive: json['isActive'] as bool? ?? true,
    );
  }
}

class CropVarietyOption {
  const CropVarietyOption({
    required this.id,
    required this.cropTypeId,
    required this.name,
    required this.isActive,
  });

  final String id;
  final String cropTypeId;
  final String name;
  final bool isActive;

  factory CropVarietyOption.fromJson(Map<String, dynamic> json) =>
      CropVarietyOption(
        id: json['id'] as String,
        cropTypeId: json['cropTypeId'] as String,
        name: json['name'] as String,
        isActive: json['isActive'] as bool? ?? true,
      );
}

class CropPlanRecord {
  const CropPlanRecord({
    required this.id,
    required this.farmId,
    required this.cropTypeId,
    required this.objective,
    required this.status,
    required this.preferredStartDate,
    required this.preferredEndDate,
    required this.budget,
    required this.createdAt,
    this.statusCode = 'unknown',
    this.statusLabel = 'Status unavailable',
    this.overallStatusCode = 'unknown',
    this.overallStatusLabel = 'Status unavailable',
    this.fieldId,
    this.cropVarietyId,
    this.cultivationSeason = 0,
  });

  final String id;
  final String farmId;
  final String? fieldId;
  final String cropTypeId;
  final String? cropVarietyId;
  final String objective;
  final int status;
  final String statusCode;
  final String statusLabel;
  final String overallStatusCode;
  final String overallStatusLabel;
  final int cultivationSeason;
  final String preferredStartDate;
  final String preferredEndDate;
  final num budget;
  final String createdAt;

  factory CropPlanRecord.fromJson(Map<String, dynamic> json) => CropPlanRecord(
    id: json['id'] as String,
    farmId: json['farmId'] as String,
    fieldId: json['fieldId'] as String?,
    cropTypeId: json['cropTypeId'] as String,
    cropVarietyId: json['cropVarietyId'] as String?,
    objective: json['objective'] as String? ?? '',
    status: json['status'] as int? ?? 0,
    statusCode: json['statusCode'] as String? ?? 'unknown',
    statusLabel: json['statusLabel'] as String? ?? 'Status unavailable',
    overallStatusCode: json['overallStatusCode'] as String? ?? 'unknown',
    overallStatusLabel:
        json['overallStatusLabel'] as String? ?? 'Status unavailable',
    cultivationSeason: json['cultivationSeason'] as int? ?? 0,
    preferredStartDate: json['preferredStartDate'] as String? ?? '',
    preferredEndDate: json['preferredEndDate'] as String? ?? '',
    budget: json['budget'] as num? ?? 0,
    createdAt: json['createdAt'] as String? ?? '',
  );

  bool get canCancel => status != 4 && status != 5 && status != 6;

  bool get isLifecycleActive => const {
    'pending',
    'ai_planning',
    'field_analysis_running',
    'weather_resource_analysis_running',
    'scheduling_validation_running',
  }.contains(statusCode);

  bool get isLifecycleFinal =>
      const {'approved', 'rejected', 'cancelled'}.contains(overallStatusCode);

  bool get isLifecycleStable => !isLifecycleActive && !isLifecycleFinal;
}

class CropPlanningStepStatus {
  const CropPlanningStepStatus({required this.stepName, required this.status});

  final String stepName;
  final int status;

  factory CropPlanningStepStatus.fromJson(Map<String, dynamic> json) =>
      CropPlanningStepStatus(
        stepName: json['stepName'] as String? ?? '',
        status: json['status'] as int? ?? 0,
      );
}

class FarmerApprovedTask {
  const FarmerApprovedTask({
    required this.id,
    required this.title,
    required this.description,
    required this.dueAt,
    required this.status,
  });
  final String id;
  final String title;
  final String description;
  final String dueAt;
  final String status;
  factory FarmerApprovedTask.fromJson(Map<String, dynamic> json) =>
      FarmerApprovedTask(
        id: json['id'] as String? ?? '',
        title: json['title'] as String? ?? '',
        description: json['description'] as String? ?? '',
        dueAt: json['dueAt'] as String? ?? '',
        status: json['status'] as String? ?? 'Approved',
      );
}

class FarmerApprovedIrrigation {
  const FarmerApprovedIrrigation({
    required this.id,
    required this.scheduledAt,
    required this.durationMinutes,
    required this.notes,
    required this.status,
  });
  final String id;
  final String scheduledAt;
  final int durationMinutes;
  final String notes;
  final String status;
  factory FarmerApprovedIrrigation.fromJson(Map<String, dynamic> json) =>
      FarmerApprovedIrrigation(
        id: json['id'] as String? ?? '',
        scheduledAt: json['scheduledAt'] as String? ?? '',
        durationMinutes: json['durationMinutes'] as int? ?? 0,
        notes: json['notes'] as String? ?? '',
        status: json['status'] as String? ?? 'Approved',
      );
}

class FarmerApprovedCropHealth {
  const FarmerApprovedCropHealth({
    required this.cropHealthObservation,
    required this.possibleConcern,
    required this.uncertaintyGuidance,
    required this.approvedPrePlantingActions,
    required this.approvedMonitoringActions,
    required this.escalationGuidance,
    required this.whyThisIsRecommended,
  });
  final String cropHealthObservation;
  final String possibleConcern;
  final String uncertaintyGuidance;
  final List<String> approvedPrePlantingActions;
  final List<String> approvedMonitoringActions;
  final String? escalationGuidance;
  final String whyThisIsRecommended;
  factory FarmerApprovedCropHealth.fromJson(Map<String, dynamic> json) =>
      FarmerApprovedCropHealth(
        cropHealthObservation: json['cropHealthObservation'] as String? ?? '',
        possibleConcern: json['possibleConcern'] as String? ?? '',
        uncertaintyGuidance: json['uncertaintyGuidance'] as String? ?? '',
        approvedPrePlantingActions:
            (json['approvedPrePlantingActions'] as List<dynamic>? ?? const [])
                .whereType<String>()
                .toList(),
        approvedMonitoringActions:
            (json['approvedMonitoringActions'] as List<dynamic>? ?? const [])
                .whereType<String>()
                .toList(),
        escalationGuidance: json['escalationGuidance'] as String?,
        whyThisIsRecommended: json['whyThisIsRecommended'] as String? ?? '',
      );
}

class FarmerApprovedPlan {
  const FarmerApprovedPlan({
    required this.contractVersion,
    required this.cropPlanRequestId,
    required this.objective,
    required this.cropName,
    required this.varietyName,
    required this.preferredStartDate,
    required this.preferredEndDate,
    required this.approvedAt,
    required this.fieldSummary,
    required this.weatherSummary,
    required this.warnings,
    required this.recommendations,
    required this.approvedTasks,
    required this.approvedIrrigationSchedules,
    required this.cropHealth,
  });
  final int contractVersion;
  final String cropPlanRequestId;
  final String objective;
  final String cropName;
  final String? varietyName;
  final String preferredStartDate;
  final String preferredEndDate;
  final String approvedAt;
  final String? fieldSummary;
  final String? weatherSummary;
  final List<String> warnings;
  final List<String> recommendations;
  final List<FarmerApprovedTask> approvedTasks;
  final List<FarmerApprovedIrrigation> approvedIrrigationSchedules;
  final FarmerApprovedCropHealth? cropHealth;

  factory FarmerApprovedPlan.fromJson(Map<String, dynamic> json) =>
      FarmerApprovedPlan(
        contractVersion: json['contractVersion'] as int? ?? 0,
        cropPlanRequestId: json['cropPlanRequestId'] as String? ?? '',
        objective: json['objective'] as String? ?? '',
        cropName: json['cropName'] as String? ?? 'Crop',
        varietyName: json['varietyName'] as String?,
        preferredStartDate: json['preferredStartDate'] as String? ?? '',
        preferredEndDate: json['preferredEndDate'] as String? ?? '',
        approvedAt: json['approvedAt'] as String? ?? '',
        fieldSummary: json['fieldSummary'] as String?,
        weatherSummary: json['weatherSummary'] as String?,
        warnings: (json['warnings'] as List<dynamic>? ?? const [])
            .whereType<String>()
            .toList(),
        recommendations: (json['recommendations'] as List<dynamic>? ?? const [])
            .whereType<String>()
            .toList(),
        approvedTasks: (json['approvedTasks'] as List<dynamic>? ?? const [])
            .whereType<Map<String, dynamic>>()
            .map(FarmerApprovedTask.fromJson)
            .toList(),
        approvedIrrigationSchedules:
            (json['approvedIrrigationSchedules'] as List<dynamic>? ?? const [])
                .whereType<Map<String, dynamic>>()
                .map(FarmerApprovedIrrigation.fromJson)
                .toList(),
        cropHealth: json['cropHealth'] is Map<String, dynamic>
            ? FarmerApprovedCropHealth.fromJson(
                json['cropHealth'] as Map<String, dynamic>,
              )
            : null,
      );
}

class CropPlanningDelegatedStep {
  const CropPlanningDelegatedStep({
    required this.sequence,
    required this.stepType,
    required this.assignedAgent,
  });

  final int sequence;
  final String stepType;
  final String assignedAgent;

  factory CropPlanningDelegatedStep.fromJson(Map<String, dynamic> json) {
    return CropPlanningDelegatedStep(
      sequence: json['sequence'] as int? ?? 0,
      stepType: json['stepType'] as String? ?? '',
      assignedAgent: json['assignedAgent'] as String? ?? '',
    );
  }
}

class CropPlanningResult {
  const CropPlanningResult({
    required this.workflowId,
    required this.status,
    required this.requiresHumanReview,
    required this.warnings,
    required this.referenceDataStatus,
    required this.objectiveSummary,
    required this.steps,
  });

  final String workflowId;
  final String status;
  final bool requiresHumanReview;
  final List<String> warnings;
  final String referenceDataStatus;
  final String objectiveSummary;
  final List<CropPlanningDelegatedStep> steps;

  factory CropPlanningResult.fromJson(Map<String, dynamic> json) {
    final warnings = json['warnings'] as List<dynamic>? ?? const [];
    final steps = json['steps'] as List<dynamic>? ?? const [];
    return CropPlanningResult(
      workflowId: json['workflowId'] as String? ?? '',
      status: json['status'] as String? ?? 'Unknown',
      requiresHumanReview: json['requiresHumanReview'] as bool? ?? false,
      warnings: warnings.map((item) => item.toString()).toList(),
      referenceDataStatus: json['referenceDataStatus'] as String? ?? 'Unknown',
      objectiveSummary: json['objectiveSummary'] as String? ?? '',
      steps: steps
          .cast<Map<String, dynamic>>()
          .map(CropPlanningDelegatedStep.fromJson)
          .toList(),
    );
  }
}

class CropPlanningWorkflowStatus {
  const CropPlanningWorkflowStatus({
    required this.workflowId,
    required this.cropPlanRequestId,
    required this.status,
    required this.currentStep,
    required this.warnings,
    this.statusCode = 'unknown',
    this.statusLabel = 'Status unavailable',
    this.overallStatusCode = 'unknown',
    this.overallStatusLabel = 'Status unavailable',
    this.steps = const [],
  });

  final String workflowId;
  final String cropPlanRequestId;
  final int status;
  final String currentStep;
  final String statusCode;
  final String statusLabel;
  final String overallStatusCode;
  final String overallStatusLabel;
  final List<String> warnings;
  final List<CropPlanningStepStatus> steps;

  factory CropPlanningWorkflowStatus.fromJson(Map<String, dynamic> json) {
    final warnings = json['warnings'] as List<dynamic>? ?? const [];
    return CropPlanningWorkflowStatus(
      workflowId: json['workflowId'] as String? ?? '',
      cropPlanRequestId: json['cropPlanRequestId'] as String? ?? '',
      status: json['status'] as int? ?? 0,
      currentStep: json['currentStep'] as String? ?? '',
      statusCode: json['statusCode'] as String? ?? 'unknown',
      statusLabel: json['statusLabel'] as String? ?? 'Status unavailable',
      overallStatusCode: json['overallStatusCode'] as String? ?? 'unknown',
      overallStatusLabel:
          json['overallStatusLabel'] as String? ?? 'Status unavailable',
      warnings: warnings.map((item) => item.toString()).toList(),
      steps: (json['steps'] as List<dynamic>? ?? const [])
          .cast<Map<String, dynamic>>()
          .map(CropPlanningStepStatus.fromJson)
          .toList(),
    );
  }
}

class PagedList<T> {
  const PagedList({
    required this.items,
    required this.page,
    required this.totalPages,
    required this.totalCount,
  });

  final List<T> items;
  final int page;
  final int totalPages;
  final int totalCount;

  bool get hasMore => page < totalPages;

  factory PagedList.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) parse,
  ) {
    final items = (json['items'] as List<dynamic>? ?? const [])
        .cast<Map<String, dynamic>>()
        .map(parse)
        .toList();
    return PagedList(
      items: items,
      page: json['page'] as int? ?? 1,
      totalPages: json['totalPages'] as int? ?? 1,
      totalCount: json['totalCount'] as int? ?? items.length,
    );
  }
}

class StockRecord {
  const StockRecord({
    required this.id,
    required this.resourceId,
    required this.resourceName,
    required this.unit,
    required this.quantityOnHand,
    required this.reservedQuantity,
    required this.availableQuantity,
    required this.lowStockThreshold,
  });

  final String id;
  final String resourceId;
  final String resourceName;
  final String unit;
  final num quantityOnHand;
  final num reservedQuantity;
  final num availableQuantity;
  final num lowStockThreshold;

  bool get isOutOfStock => availableQuantity <= 0;
  bool get isLowStock =>
      !isOutOfStock && availableQuantity <= lowStockThreshold;

  factory StockRecord.fromJson(Map<String, dynamic> json) {
    return StockRecord(
      id: json['id'] as String,
      resourceId: json['resourceId'] as String,
      resourceName: json['resourceName'] as String? ?? 'Unnamed resource',
      unit: json['unit'] as String? ?? '',
      quantityOnHand: json['quantityOnHand'] as num,
      reservedQuantity: json['reservedQuantity'] as num,
      availableQuantity: json['availableQuantity'] as num,
      lowStockThreshold: json['lowStockThreshold'] as num,
    );
  }
}

/// Backend ResourceReservationStatus: 1 Active, 2 Released, 3 Cancelled.
class ReservationRecord {
  const ReservationRecord({
    required this.id,
    required this.inventoryStockId,
    required this.requestedByUserId,
    required this.quantity,
    required this.status,
    required this.purpose,
    required this.resourceName,
    required this.unit,
    required this.createdAt,
    this.releasedAt,
  });

  static const statusActive = 1;
  static const statusReleased = 2;
  static const statusCancelled = 3;

  final String id;
  final String inventoryStockId;
  final String requestedByUserId;
  final num quantity;
  final int status;
  final String purpose;
  final String resourceName;
  final String unit;
  final String createdAt;
  final String? releasedAt;

  bool get isActive => status == statusActive;

  String get statusLabel => switch (status) {
    statusActive => 'Reserved',
    statusReleased => 'Released',
    statusCancelled => 'Cancelled',
    _ => 'Unknown',
  };

  factory ReservationRecord.fromJson(Map<String, dynamic> json) {
    return ReservationRecord(
      id: json['id'] as String,
      inventoryStockId: json['inventoryStockId'] as String,
      requestedByUserId: json['requestedByUserId'] as String,
      quantity: json['quantity'] as num,
      status: json['status'] as int,
      purpose: json['purpose'] as String? ?? '',
      resourceName: json['resourceName'] as String? ?? 'Unnamed resource',
      unit: json['unit'] as String? ?? '',
      createdAt: json['createdAt'] as String? ?? '',
      releasedAt: json['releasedAt'] as String?,
    );
  }
}

/// Backend StockTransactionType: 1 Add, 2 Remove, 3 Reserve, 4 Release.
class StockTransactionRecord {
  const StockTransactionRecord({
    required this.id,
    required this.type,
    required this.quantity,
    required this.note,
    required this.createdAt,
  });

  final String id;
  final int type;
  final num quantity;
  final String note;
  final String createdAt;

  String get typeLabel => switch (type) {
    1 => 'Stock added',
    2 => 'Stock removed',
    3 => 'Reserved',
    4 => 'Released',
    _ => 'Stock change',
  };

  factory StockTransactionRecord.fromJson(Map<String, dynamic> json) {
    return StockTransactionRecord(
      id: json['id'] as String,
      type: json['type'] as int,
      quantity: json['quantity'] as num,
      note: json['note'] as String? ?? '',
      createdAt: json['createdAt'] as String? ?? '',
    );
  }
}
