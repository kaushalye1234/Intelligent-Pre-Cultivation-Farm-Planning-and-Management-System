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
  });

  final String id;
  final String title;
  final String description;
  final String dueAt;
  final int status;

  factory FarmTaskRecord.fromJson(Map<String, dynamic> json) => FarmTaskRecord(
    id: json['id'] as String,
    title: json['title'] as String? ?? '',
    description: json['description'] as String? ?? '',
    dueAt: json['dueAt'] as String? ?? '',
    status: json['status'] as int? ?? 0,
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
  });

  final String id;
  final String fieldId;
  final String scheduledAt;
  final int durationMinutes;
  final String notes;
  final int status;

  factory IrrigationScheduleRecord.fromJson(Map<String, dynamic> json) =>
      IrrigationScheduleRecord(
        id: json['id'] as String,
        fieldId: json['fieldId'] as String,
        scheduledAt: json['scheduledAt'] as String? ?? '',
        durationMinutes: json['durationMinutes'] as int? ?? 0,
        notes: json['notes'] as String? ?? '',
        status: json['status'] as int? ?? 0,
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
  const FieldOption({required this.id, required this.name});

  final String id;
  final String name;

  factory FieldOption.fromJson(Map<String, dynamic> json) {
    return FieldOption(id: json['id'] as String, name: json['name'] as String);
  }
}

class CropTypeOption {
  const CropTypeOption({required this.id, required this.name});

  final String id;
  final String name;

  factory CropTypeOption.fromJson(Map<String, dynamic> json) {
    return CropTypeOption(
      id: json['id'] as String,
      name: json['name'] as String,
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
  });

  final String workflowId;
  final String cropPlanRequestId;
  final int status;
  final String currentStep;
  final List<String> warnings;

  factory CropPlanningWorkflowStatus.fromJson(Map<String, dynamic> json) {
    final warnings = json['warnings'] as List<dynamic>? ?? const [];
    return CropPlanningWorkflowStatus(
      workflowId: json['workflowId'] as String? ?? '',
      cropPlanRequestId: json['cropPlanRequestId'] as String? ?? '',
      status: json['status'] as int? ?? 0,
      currentStep: json['currentStep'] as String? ?? '',
      warnings: warnings.map((item) => item.toString()).toList(),
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
