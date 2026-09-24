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
    this.totalArea = 0,
  });

  final String id;
  final String name;
  final String location;
  final num totalArea;

  factory FarmOption.fromJson(Map<String, dynamic> json) {
    return FarmOption(
      id: json['id'] as String,
      name: json['name'] as String,
      location: json['location'] as String? ?? '',
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
    cultivationSeason: json['cultivationSeason'] as int? ?? 0,
    preferredStartDate: json['preferredStartDate'] as String? ?? '',
    preferredEndDate: json['preferredEndDate'] as String? ?? '',
    budget: json['budget'] as num? ?? 0,
    createdAt: json['createdAt'] as String? ?? '',
  );

  String get statusLabel => switch (status) {
    1 => 'Draft',
    2 => 'Submitted',
    3 => 'Preliminary plan',
    4 => 'Approved',
    5 => 'Rejected',
    6 => 'Cancelled',
    _ => 'Unknown',
  };
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

class ApprovedWorkflowDetail {
  const ApprovedWorkflowDetail({
    required this.workflowId,
    required this.status,
    required this.approvedAt,
    required this.fieldSummary,
    required this.weatherSummary,
    required this.warnings,
    required this.recommendations,
  });

  final String workflowId;
  final int status;
  final String? approvedAt;
  final String? fieldSummary;
  final String? weatherSummary;
  final List<String> warnings;
  final List<String> recommendations;

  factory ApprovedWorkflowDetail.fromJson(Map<String, dynamic> json) {
    final workflow = json['workflow'] as Map<String, dynamic>? ?? const {};
    final decisions = (json['decisions'] as List<dynamic>? ?? const [])
        .cast<Map<String, dynamic>>();
    final steps = (json['steps'] as List<dynamic>? ?? const [])
        .cast<Map<String, dynamic>>();
    String? summaryFor(String agentName, String key) {
      for (final step in steps.reversed) {
        if (step['agentName'] == agentName && step['status'] == 3) {
          final output = step['output'];
          if (output is Map<String, dynamic>) {
            final value = output[key];
            if (value is String && value.isNotEmpty) return value;
            if (value is Map<String, dynamic>) {
              final summary = value['summary'];
              if (summary is String && summary.isNotEmpty) return summary;
            }
          }
        }
      }
      return null;
    }

    String? approvedAt;
    for (final decision in decisions.reversed) {
      if (decision['decision'] == 1) {
        approvedAt = decision['createdAt'] as String?;
        break;
      }
    }
    final warnings = <String>[];
    final recommendations = <String>[];
    for (final step in steps) {
      if (step['status'] != 3) continue;
      final output = step['output'];
      if (output is! Map<String, dynamic>) continue;
      warnings.addAll(
        (output['warnings'] as List<dynamic>? ?? const []).whereType<String>(),
      );
      if (step['agentName'] == 'WeatherResourceAgent') {
        recommendations.addAll(
          (output['recommendations'] as List<dynamic>? ?? const [])
              .whereType<String>(),
        );
      }
    }
    return ApprovedWorkflowDetail(
      workflowId: workflow['id'] as String? ?? '',
      status: workflow['status'] as int? ?? 0,
      approvedAt: approvedAt,
      fieldSummary: summaryFor('CropFieldAnalysisAgent', 'fieldCondition'),
      weatherSummary: summaryFor('WeatherResourceAgent', 'weatherSummary'),
      warnings: warnings.toSet().toList(),
      recommendations: recommendations.toSet().toList(),
    );
  }
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
    this.steps = const [],
  });

  final String workflowId;
  final String cropPlanRequestId;
  final int status;
  final String currentStep;
  final List<String> warnings;
  final List<CropPlanningStepStatus> steps;

  factory CropPlanningWorkflowStatus.fromJson(Map<String, dynamic> json) {
    final warnings = json['warnings'] as List<dynamic>? ?? const [];
    return CropPlanningWorkflowStatus(
      workflowId: json['workflowId'] as String? ?? '',
      cropPlanRequestId: json['cropPlanRequestId'] as String? ?? '',
      status: json['status'] as int? ?? 0,
      currentStep: json['currentStep'] as String? ?? '',
      warnings: warnings.map((item) => item.toString()).toList(),
      steps: (json['steps'] as List<dynamic>? ?? const [])
          .cast<Map<String, dynamic>>()
          .map(CropPlanningStepStatus.fromJson)
          .toList(),
    );
  }

  String get statusLabel {
    return switch (status) {
      1 => 'Not started',
      2 => 'Pending',
      3 => 'Running',
      4 => 'Completed',
      5 => 'Failed',
      6 => 'Cancelled',
      7 => 'Candidate ready',
      8 => 'Awaiting officer approval',
      9 => 'Rejected',
      10 => 'Revision requested',
      11 => 'Waiting for required data',
      _ => 'Unknown',
    };
  }
}

class CropPlanningWorkflowStart {
  const CropPlanningWorkflowStart({
    required this.workflowId,
    required this.cropPlanRequestId,
    required this.status,
    required this.requiresHumanReview,
    required this.warnings,
  });

  final String workflowId;
  final String cropPlanRequestId;
  final String status;
  final bool requiresHumanReview;
  final List<String> warnings;

  factory CropPlanningWorkflowStart.fromJson(Map<String, dynamic> json) {
    final warnings = json['warnings'] as List<dynamic>? ?? const [];
    return CropPlanningWorkflowStart(
      workflowId: json['workflowId'] as String? ?? '',
      cropPlanRequestId: json['cropPlanRequestId'] as String? ?? '',
      status: json['status'] as String? ?? 'Unknown',
      requiresHumanReview: json['requiresHumanReview'] as bool? ?? false,
      warnings: warnings.map((item) => item.toString()).toList(),
    );
  }
}
